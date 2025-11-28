using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;
using PokeSharp.Engine.UI.Debug.Utilities;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
/// Enhanced input field specifically for command-line interfaces.
/// Supports command history, auto-completion, and multi-line input.
/// </summary>
public class CommandInput : UIComponent, ITextInput
{
    private string _text = string.Empty;
    private int _cursorPosition = 0;
    private float _cursorBlinkTimer = 0;
    private static float CursorBlinkRate => 0.5f; // Fixed value, doesn't need theme

    // Selection
    private int _selectionStart = 0;
    private int _selectionEnd = 0;
    private bool _hasSelection = false;

    // Command history
    private readonly List<string> _history = new();
    private int _historyIndex = -1; // -1 = not navigating history
    private string _temporaryInput = string.Empty; // Stores current input when navigating history
    private int _maxHistory = 100;

    // Multi-line support
    private bool _multiLineMode = false;

    // Visual properties - nullable for theme fallback
    private Color? _backgroundColor;
    private Color? _textColor;
    private Color? _cursorColor;
    private Color? _selectionColor;
    private Color? _borderColor;
    private Color? _focusBorderColor;
    private Color? _promptColor;

    public Color BackgroundColor { get => _backgroundColor ?? ThemeManager.Current.InputBackground; set => _backgroundColor = value; }
    public Color TextColor { get => _textColor ?? ThemeManager.Current.InputText; set => _textColor = value; }
    public Color CursorColor { get => _cursorColor ?? ThemeManager.Current.InputCursor; set => _cursorColor = value; }
    public Color SelectionColor { get => _selectionColor ?? ThemeManager.Current.InputSelection; set => _selectionColor = value; }
    public Color BorderColor { get => _borderColor ?? ThemeManager.Current.BorderPrimary; set => _borderColor = value; }
    public Color FocusBorderColor { get => _focusBorderColor ?? ThemeManager.Current.BorderFocus; set => _focusBorderColor = value; }
    public float BorderThickness { get; set; } = 1;
    public float Padding { get; set; } = 8f;

    // Prompt string (e.g., " ")
    public string Prompt { get; set; } = Core.NerdFontIcons.Prompt;
    public Color PromptColor { get => _promptColor ?? ThemeManager.Current.Prompt; set => _promptColor = value; }

    // Properties
    public string Text => _text;
    public int CursorPosition => _cursorPosition;
    public bool HasSelection => _hasSelection;
    public string SelectedText => _hasSelection ? _text.Substring(SelectionStart, SelectionLength) : string.Empty;
    public bool IsMultiLine { get => _multiLineMode; set => _multiLineMode = value; }

    private int SelectionStart => Math.Min(_selectionStart, _selectionEnd);
    private int SelectionEnd => Math.Max(_selectionStart, _selectionEnd);
    private int SelectionLength => SelectionEnd - SelectionStart;

    // Events (ITextInput interface)
    public event Action<string>? OnSubmit;
    public event Action<string>? OnTextChanged;

    // Additional events (not in ITextInput)
    public Action<string>? OnRequestCompletions { get; set; }
    public Action? OnEscape { get; set; }

    public CommandInput(string id) { Id = id; }

    /// <summary>
    /// Requests focus for this input component (ITextInput interface).
    /// </summary>
    public void Focus()
    {
        Context?.SetFocus(Id);
    }

    /// <summary>
    /// Sets the input text programmatically.
    /// </summary>
    public void SetText(string text)
    {
        _text = text;
        _cursorPosition = Math.Clamp(_cursorPosition, 0, _text.Length);
        OnTextChanged?.Invoke(_text);
    }

    /// <summary>
    /// Completes the current word at the cursor position with the given completion text.
    /// Finds the word boundary before the cursor and replaces it.
    /// </summary>
    public void CompleteText(string completionText)
    {
        // Find the start of the current word (go back until we hit a word boundary)
        int wordStart = _cursorPosition;
        while (wordStart > 0)
        {
            char c = _text[wordStart - 1];
            // Word boundary: whitespace, operators, or dot (for member access like Console.WriteLine)
            if (char.IsWhiteSpace(c) || c == '(' || c == ')' || c == '[' || c == ']' ||
                c == '{' || c == '}' || c == ',' || c == ';' || c == '=' || c == '.')
            {
                break;
            }
            wordStart--;
        }

        // Remove the partial word
        if (wordStart < _cursorPosition)
        {
            _text = _text.Remove(wordStart, _cursorPosition - wordStart);
            _cursorPosition = wordStart;
        }

        // Insert the completion
        _text = _text.Insert(_cursorPosition, completionText);
        _cursorPosition += completionText.Length;

        _historyIndex = -1; // Reset history navigation

        OnTextChanged?.Invoke(_text);
    }

    /// <summary>
    /// Clears the input field.
    /// </summary>
    public void Clear()
    {
        _text = string.Empty;
        _cursorPosition = 0;
        ClearSelection();
        _historyIndex = -1;
        _temporaryInput = string.Empty;
        OnTextChanged?.Invoke(_text);
    }

    /// <summary>
    /// Submits the current input.
    /// </summary>
    public void Submit()
    {
        if (string.IsNullOrWhiteSpace(_text))
            return;

        // Add to history
        if (_history.Count == 0 || _history[^1] != _text)
        {
            _history.Add(_text);

            // Limit history size
            while (_history.Count > _maxHistory)
            {
                _history.RemoveAt(0);
            }
        }

        // Fire submit event
        OnSubmit?.Invoke(_text);

        // Clear input
        Clear();
    }

    /// <summary>
    /// Navigates to the previous command in history.
    /// </summary>
    public void HistoryPrevious()
    {
        if (_history.Count == 0)
            return;

        // Save current input if we're starting history navigation
        if (_historyIndex == -1)
        {
            _temporaryInput = _text;
            _historyIndex = _history.Count;
        }

        _historyIndex = Math.Max(0, _historyIndex - 1);
        SetText(_history[_historyIndex]);
        _cursorPosition = _text.Length;
    }

    /// <summary>
    /// Navigates to the next command in history.
    /// </summary>
    public void HistoryNext()
    {
        if (_historyIndex == -1)
            return;

        _historyIndex++;

        if (_historyIndex >= _history.Count)
        {
            // Restore temporary input
            SetText(_temporaryInput);
            _historyIndex = -1;
            _temporaryInput = string.Empty;
        }
        else
        {
            SetText(_history[_historyIndex]);
        }

        _cursorPosition = _text.Length;
    }

    /// <summary>
    /// Loads command history from a list.
    /// </summary>
    public void LoadHistory(List<string> history)
    {
        _history.Clear();
        _history.AddRange(history);
    }

    /// <summary>
    /// Gets the current command history.
    /// </summary>
    public List<string> GetHistory() => new List<string>(_history);

    private void ClearSelection()
    {
        _hasSelection = false;
        _selectionStart = 0;
        _selectionEnd = 0;
    }

    private void InsertText(string text)
    {
        if (_hasSelection)
        {
            // Replace selection
            _text = _text.Remove(SelectionStart, SelectionLength);
            _cursorPosition = SelectionStart;
            ClearSelection();
        }

        _text = _text.Insert(_cursorPosition, text);
        _cursorPosition += text.Length;
        _historyIndex = -1; // Reset history navigation
        OnTextChanged?.Invoke(_text);
    }

    private void DeleteBackward()
    {
        if (_hasSelection)
        {
            _text = _text.Remove(SelectionStart, SelectionLength);
            _cursorPosition = SelectionStart;
            ClearSelection();
        }
        else if (_cursorPosition > 0)
        {
            _text = _text.Remove(_cursorPosition - 1, 1);
            _cursorPosition--;
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(_text);
    }

    private void DeleteForward()
    {
        if (_hasSelection)
        {
            _text = _text.Remove(SelectionStart, SelectionLength);
            _cursorPosition = SelectionStart;
            ClearSelection();
        }
        else if (_cursorPosition < _text.Length)
        {
            _text = _text.Remove(_cursorPosition, 1);
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(_text);
    }

    // Mouse drag state for selection
    private bool _isMouseDragging = false;
    private bool _isSelectingWithMouse = false;

    protected override void OnRender(UIContext context)
    {
        var input = context.Input;
        var mousePos = input.MousePosition;

        // Handle mouse input for focus, cursor positioning, and selection
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

        // Calculate text position
        var textPos = new Vector2(resolvedRect.X + Padding, resolvedRect.Y + Padding);

        // Draw prompt
        renderer.DrawText(Prompt, textPos, PromptColor);
        var promptWidth = renderer.MeasureText(Prompt).X;
        textPos.X += promptWidth;

        // Draw selection background
        if (_hasSelection)
        {
            var beforeSelection = _text.Substring(0, SelectionStart);
            var selectedText = _text.Substring(SelectionStart, SelectionLength);

            var beforeWidth = renderer.MeasureText(beforeSelection).X;
            var selectionWidth = renderer.MeasureText(selectedText).X;

            var selectionRect = new LayoutRect(
                textPos.X + beforeWidth,
                textPos.Y,
                selectionWidth,
                ThemeManager.Current.LineHeight
            );
            renderer.DrawRectangle(selectionRect, SelectionColor);
        }

        // Draw text with syntax highlighting
        if (!string.IsNullOrEmpty(_text))
        {
            var segments = SyntaxHighlighter.Highlight(_text);
            float currentX = textPos.X;

            foreach (var segment in segments)
            {
                renderer.DrawText(segment.Text, new Vector2(currentX, textPos.Y), segment.Color);
                currentX += renderer.MeasureText(segment.Text).X;
            }
        }

        // Draw cursor (using base class IsFocused() method)
        if (IsFocused())
        {
            _cursorBlinkTimer += (float)context.Input.GameTime.ElapsedGameTime.TotalSeconds;
            if (_cursorBlinkTimer > CursorBlinkRate)
                _cursorBlinkTimer = 0;

            if (_cursorBlinkTimer < CursorBlinkRate / 2)
            {
                var textBeforeCursor = _text.Substring(0, _cursorPosition);
                var cursorX = textPos.X + renderer.MeasureText(textBeforeCursor).X;

                var cursorRect = new LayoutRect(
                    cursorX,
                    textPos.Y,
                    2,
                    ThemeManager.Current.LineHeight
                );
                renderer.DrawRectangle(cursorRect, CursorColor);
            }
        }
    }

    /// <summary>
    /// Handles mouse click to position cursor at the clicked character.
    /// Uses binary search for efficiency with long text.
    /// </summary>
    /// <param name="mousePos">Mouse position</param>
    /// <param name="renderer">UI renderer for text measurement</param>
    /// <param name="extendSelection">If true (Shift held), extends selection instead of clearing</param>
    private void HandleMouseClick(Point mousePos, UIRenderer renderer, bool extendSelection = false)
    {
        int newPosition;

        if (string.IsNullOrEmpty(_text))
        {
            newPosition = 0;
        }
        else
        {
            // Calculate text start position (after padding and prompt)
            var promptWidth = renderer.MeasureText(Prompt).X;
            float textStartX = Rect.X + Padding + promptWidth;
            float relativeX = mousePos.X - textStartX;

            // Click before text start
            if (relativeX <= 0)
            {
                newPosition = 0;
            }
            // Click after text end
            else
            {
                float totalWidth = renderer.MeasureText(_text).X;
                if (relativeX >= totalWidth)
                {
                    newPosition = _text.Length;
                }
                else
                {
                    // Binary search to find character position
                    newPosition = FindCharacterPosition(relativeX, renderer);
                }
            }
        }

        // Handle selection
        if (extendSelection)
        {
            // Shift+Click: extend selection from current position (or start new selection)
            if (!_hasSelection)
            {
                _hasSelection = true;
                _selectionStart = _cursorPosition;
            }
            _selectionEnd = newPosition;
        }
        else
        {
            // Normal click: clear selection and set selection anchor for potential drag
            ClearSelection();
            _selectionStart = newPosition;
            _selectionEnd = newPosition;
        }

        _cursorPosition = newPosition;
    }

    protected override bool IsInteractive() => true;

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
                // Set focus immediately on press (not release)
                context.SetFocus(Id);

                // Capture input for potential drag selection
                context.CaptureInput(Id);
                _isMouseDragging = true;

                // Position cursor (Shift+Click extends selection)
                HandleMouseClick(mousePos, context.Renderer, input.IsShiftDown());

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

        // Mouse dragging - extend selection
        if (_isMouseDragging && input.IsMouseButtonDown(MouseButton.Left))
        {
            // Continue selection even if mouse moves outside bounds (input is captured)
            HandleMouseDrag(mousePos, context.Renderer);
        }

        // Mouse button released - end drag
        if (input.IsMouseButtonReleased(MouseButton.Left))
        {
            if (_isMouseDragging)
            {
                _isMouseDragging = false;
                _isSelectingWithMouse = false;
                context.ReleaseCapture();
            }
        }
    }

    /// <summary>
    /// Handles mouse drag to extend selection.
    /// </summary>
    private void HandleMouseDrag(Point mousePos, UIRenderer renderer)
    {
        if (string.IsNullOrEmpty(_text))
            return;

        // Calculate new cursor position from mouse
        var promptWidth = renderer.MeasureText(Prompt).X;
        float textStartX = Rect.X + Padding + promptWidth;
        float relativeX = mousePos.X - textStartX;

        int newPosition;
        if (relativeX <= 0)
        {
            newPosition = 0;
        }
        else
        {
            float totalWidth = renderer.MeasureText(_text).X;
            if (relativeX >= totalWidth)
            {
                newPosition = _text.Length;
            }
            else
            {
                // Binary search for position
                newPosition = FindCharacterPosition(relativeX, renderer);
            }
        }

        // If this is the first drag movement, start selection
        if (!_isSelectingWithMouse && newPosition != _cursorPosition)
        {
            _isSelectingWithMouse = true;
            if (!_hasSelection)
            {
                _hasSelection = true;
                _selectionStart = _cursorPosition;
            }
        }

        // Update cursor and selection end
        if (_isSelectingWithMouse)
        {
            _cursorPosition = newPosition;
            _selectionEnd = newPosition;
        }
    }

    /// <summary>
    /// Binary search to find character position from X coordinate.
    /// </summary>
    private int FindCharacterPosition(float relativeX, UIRenderer renderer)
    {
        int left = 0;
        int right = _text.Length;

        while (left < right)
        {
            int mid = (left + right) / 2;
            string substring = _text.Substring(0, mid);
            float widthAtMid = renderer.MeasureText(substring).X;

            if (widthAtMid < relativeX)
            {
                left = mid + 1;
            }
            else
            {
                right = mid;
            }
        }

        // Check if we should round to previous character
        if (left > 0)
        {
            string substringAtLeft = _text.Substring(0, left);
            string substringAtPrev = _text.Substring(0, left - 1);
            float widthAtLeft = renderer.MeasureText(substringAtLeft).X;
            float widthAtPrev = renderer.MeasureText(substringAtPrev).X;
            float midPoint = (widthAtPrev + widthAtLeft) / 2;

            if (relativeX < midPoint)
            {
                left--;
            }
        }

        return left;
    }

    private void HandleKeyboardInput(InputState input)
    {
        // Enter - Submit (parent component handles suggestions)
        if (input.IsKeyPressed(Keys.Enter))
        {
            // In the new architecture, parent (ConsolePanel) handles suggestion acceptance
            // We just handle command submission
            if (!_multiLineMode || !input.IsShiftDown())
            {
                Submit();
            }
            else
            {
                InsertText("\n");
            }
            return;
        }

        // Escape (parent component handles suggestions)
        if (input.IsKeyPressed(Keys.Escape))
        {
            OnEscape?.Invoke();
            return;
        }

        // Up/Down arrows (parent handles suggestions, we handle history)
        if (input.IsKeyPressed(Keys.Up))
        {
            HistoryPrevious();
            return;
        }

        if (input.IsKeyPressed(Keys.Down))
        {
            HistoryNext();
            return;
        }

        // Tab - Auto-complete
        if (input.IsKeyPressed(Keys.Tab))
        {
            OnRequestCompletions?.Invoke(_text);
            return;
        }

        // Backspace - with repeat
        if (input.IsKeyPressedWithRepeat(Keys.Back))
        {
            DeleteBackward();
            return;
        }

        // Delete - with repeat
        if (input.IsKeyPressedWithRepeat(Keys.Delete))
        {
            DeleteForward();
            return;
        }

        // Left/Right arrows - with repeat
        if (input.IsKeyPressedWithRepeat(Keys.Left))
        {
            _cursorPosition = Math.Max(0, _cursorPosition - 1);
            ClearSelection();
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.Right))
        {
            _cursorPosition = Math.Min(_text.Length, _cursorPosition + 1);
            ClearSelection();
            return;
        }

        // Home/End - with repeat
        if (input.IsKeyPressedWithRepeat(Keys.Home))
        {
            _cursorPosition = 0;
            ClearSelection();
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.End))
        {
            _cursorPosition = _text.Length;
            ClearSelection();
            return;
        }

        // Ctrl+A - Select all
        if (input.IsCtrlDown() && input.IsKeyPressed(Keys.A))
        {
            _hasSelection = true;
            _selectionStart = 0;
            _selectionEnd = _text.Length;
            return;
        }

        // Ctrl+C - Copy
        if (input.IsCtrlDown() && input.IsKeyPressed(Keys.C))
        {
            if (_hasSelection)
            {
                // Would need clipboard access
                // SDL2.SDL.SDL_SetClipboardText(SelectedText);
            }
            return;
        }

        // Ctrl+V - Paste
        if (input.IsCtrlDown() && input.IsKeyPressed(Keys.V))
        {
            // Would need clipboard access
            // var clipboardText = SDL2.SDL.SDL_GetClipboardText();
            // InsertText(clipboardText);
            return;
        }

        // Regular character input - with repeat for smooth typing when held
        foreach (var key in Enum.GetValues<Keys>())
        {
            if (input.IsKeyPressedWithRepeat(key))
            {
                var ch = Utilities.KeyboardHelper.KeyToChar(key, input.IsShiftDown());
                if (ch.HasValue)
                {
                    InsertText(ch.Value.ToString());
                }
            }
        }
    }

}

