using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Game.Engine.UI.Debug.Components.Base;
using PokeSharp.Game.Engine.UI.Debug.Core;
using PokeSharp.Game.Engine.UI.Debug.Input;
using PokeSharp.Game.Engine.UI.Debug.Layout;
using PokeSharp.Game.Engine.UI.Debug.Utilities;

namespace PokeSharp.Game.Engine.UI.Debug.Components.Controls;

/// <summary>
///     Enhanced input field specifically for command-line interfaces.
///     Supports command history, auto-completion, and multi-line input.
/// </summary>
public class CommandInput : UIComponent, ITextInput
{
    // Command history
    private readonly List<string> _history = new();
    private readonly int _maxHistory = 100;

    // Visual properties - nullable for theme fallback
    private Color? _backgroundColor;
    private Color? _borderColor;
    private float _cursorBlinkTimer;
    private Color? _cursorColor;
    private Color? _focusBorderColor;
    private int _historyIndex = -1; // -1 = not navigating history

    // Mouse drag state for selection
    private bool _isMouseDragging;
    private bool _isSelectingWithMouse;

    // Multi-line support
    private Color? _promptColor;
    private Color? _selectionColor;
    private int _selectionEnd;

    // Selection
    private int _selectionStart;
    private string _temporaryInput = string.Empty; // Stores current input when navigating history
    private Color? _textColor;

    public CommandInput(string id)
    {
        Id = id;
    }

    private static float CursorBlinkRate => 0.5f; // Fixed value, doesn't need theme

    public Color BackgroundColor
    {
        get => _backgroundColor ?? ThemeManager.Current.InputBackground;
        set => _backgroundColor = value;
    }

    public Color TextColor
    {
        get => _textColor ?? ThemeManager.Current.InputText;
        set => _textColor = value;
    }

    public Color CursorColor
    {
        get => _cursorColor ?? ThemeManager.Current.InputCursor;
        set => _cursorColor = value;
    }

    public Color SelectionColor
    {
        get => _selectionColor ?? ThemeManager.Current.InputSelection;
        set => _selectionColor = value;
    }

    public Color BorderColor
    {
        get => _borderColor ?? ThemeManager.Current.BorderPrimary;
        set => _borderColor = value;
    }

    public Color FocusBorderColor
    {
        get => _focusBorderColor ?? ThemeManager.Current.BorderFocus;
        set => _focusBorderColor = value;
    }

    public float BorderThickness { get; set; } = 1;
    public float Padding { get; set; } = 8f;

    // Prompt string (e.g., " ")
    public string Prompt { get; set; } = NerdFontIcons.Prompt;

    public Color PromptColor
    {
        get => _promptColor ?? ThemeManager.Current.Prompt;
        set => _promptColor = value;
    }

    public bool IsMultiLine { get; set; }

    private int SelectionStart => Math.Min(_selectionStart, _selectionEnd);
    private int SelectionEnd => Math.Max(_selectionStart, _selectionEnd);
    private int SelectionLength => SelectionEnd - SelectionStart;

    // Additional events (not in ITextInput)
    public Action<string>? OnRequestCompletions { get; set; }
    public Action? OnEscape { get; set; }

    // Properties
    public string Text { get; private set; } = string.Empty;

    public int CursorPosition { get; private set; }

    public bool HasSelection { get; private set; }

    public string SelectedText =>
        HasSelection ? Text.Substring(SelectionStart, SelectionLength) : string.Empty;

    // Events (ITextInput interface)
    public event Action<string>? OnSubmit;
    public event Action<string>? OnTextChanged;

    /// <summary>
    ///     Requests focus for this input component (ITextInput interface).
    /// </summary>
    public void Focus()
    {
        Context?.SetFocus(Id);
    }

    /// <summary>
    ///     Sets the input text programmatically.
    /// </summary>
    public void SetText(string text)
    {
        Text = text;
        CursorPosition = Math.Clamp(CursorPosition, 0, Text.Length);
        OnTextChanged?.Invoke(Text);
    }

    /// <summary>
    ///     Clears the input field.
    /// </summary>
    public void Clear()
    {
        Text = string.Empty;
        CursorPosition = 0;
        ClearSelection();
        _historyIndex = -1;
        _temporaryInput = string.Empty;
        OnTextChanged?.Invoke(Text);
    }

    /// <summary>
    ///     Completes the current word at the cursor position with the given completion text.
    ///     Finds the word boundary before the cursor and replaces it.
    /// </summary>
    public void CompleteText(string completionText)
    {
        // Find the start of the current word (go back until we hit a word boundary)
        int wordStart = CursorPosition;
        while (wordStart > 0)
        {
            char c = Text[wordStart - 1];
            // Word boundary: whitespace, operators, or dot (for member access like Console.WriteLine)
            if (
                char.IsWhiteSpace(c)
                || c == '('
                || c == ')'
                || c == '['
                || c == ']'
                || c == '{'
                || c == '}'
                || c == ','
                || c == ';'
                || c == '='
                || c == '.'
            )
            {
                break;
            }

            wordStart--;
        }

        // Remove the partial word
        if (wordStart < CursorPosition)
        {
            Text = Text.Remove(wordStart, CursorPosition - wordStart);
            CursorPosition = wordStart;
        }

        // Insert the completion
        Text = Text.Insert(CursorPosition, completionText);
        CursorPosition += completionText.Length;

        _historyIndex = -1; // Reset history navigation

        OnTextChanged?.Invoke(Text);
    }

    /// <summary>
    ///     Submits the current input.
    /// </summary>
    public void Submit()
    {
        if (string.IsNullOrWhiteSpace(Text))
        {
            return;
        }

        // Add to history
        if (_history.Count == 0 || _history[^1] != Text)
        {
            _history.Add(Text);

            // Limit history size
            while (_history.Count > _maxHistory)
            {
                _history.RemoveAt(0);
            }
        }

        // Fire submit event
        OnSubmit?.Invoke(Text);

        // Clear input
        Clear();
    }

    /// <summary>
    ///     Navigates to the previous command in history.
    /// </summary>
    public void HistoryPrevious()
    {
        if (_history.Count == 0)
        {
            return;
        }

        // Save current input if we're starting history navigation
        if (_historyIndex == -1)
        {
            _temporaryInput = Text;
            _historyIndex = _history.Count;
        }

        _historyIndex = Math.Max(0, _historyIndex - 1);
        SetText(_history[_historyIndex]);
        CursorPosition = Text.Length;
    }

    /// <summary>
    ///     Navigates to the next command in history.
    /// </summary>
    public void HistoryNext()
    {
        if (_historyIndex == -1)
        {
            return;
        }

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

        CursorPosition = Text.Length;
    }

    /// <summary>
    ///     Loads command history from a list.
    /// </summary>
    public void LoadHistory(List<string> history)
    {
        _history.Clear();
        _history.AddRange(history);
    }

    /// <summary>
    ///     Gets the current command history.
    /// </summary>
    public List<string> GetHistory()
    {
        return new List<string>(_history);
    }

    private void ClearSelection()
    {
        HasSelection = false;
        _selectionStart = 0;
        _selectionEnd = 0;
    }

    private void InsertText(string text)
    {
        if (HasSelection)
        {
            // Replace selection
            Text = Text.Remove(SelectionStart, SelectionLength);
            CursorPosition = SelectionStart;
            ClearSelection();
        }

        Text = Text.Insert(CursorPosition, text);
        CursorPosition += text.Length;
        _historyIndex = -1; // Reset history navigation
        OnTextChanged?.Invoke(Text);
    }

    private void DeleteBackward()
    {
        if (HasSelection)
        {
            Text = Text.Remove(SelectionStart, SelectionLength);
            CursorPosition = SelectionStart;
            ClearSelection();
        }
        else if (CursorPosition > 0)
        {
            Text = Text.Remove(CursorPosition - 1, 1);
            CursorPosition--;
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    private void DeleteForward()
    {
        if (HasSelection)
        {
            Text = Text.Remove(SelectionStart, SelectionLength);
            CursorPosition = SelectionStart;
            ClearSelection();
        }
        else if (CursorPosition < Text.Length)
        {
            Text = Text.Remove(CursorPosition, 1);
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    protected override void OnRender(UIContext context)
    {
        InputState input = context.Input;
        Point mousePos = input.MousePosition;

        // Handle mouse input for focus, cursor positioning, and selection
        HandleMouseInput(context, input, mousePos);

        // Handle keyboard input if focused
        if (IsFocused())
        {
            HandleKeyboardInput(context.Input);
        }

        UIRenderer renderer = Renderer;
        LayoutRect resolvedRect = Rect;

        // Draw background
        renderer.DrawRectangle(resolvedRect, BackgroundColor);

        // Draw border
        Color borderColor = IsFocused() ? FocusBorderColor : BorderColor;
        renderer.DrawRectangleOutline(resolvedRect, borderColor, (int)BorderThickness);

        // Calculate text position
        var textPos = new Vector2(resolvedRect.X + Padding, resolvedRect.Y + Padding);

        // Draw prompt
        renderer.DrawText(Prompt, textPos, PromptColor);
        float promptWidth = renderer.MeasureText(Prompt).X;
        textPos.X += promptWidth;

        // Draw selection background
        if (HasSelection)
        {
            string beforeSelection = Text.Substring(0, SelectionStart);
            string selectedText = Text.Substring(SelectionStart, SelectionLength);

            float beforeWidth = renderer.MeasureText(beforeSelection).X;
            float selectionWidth = renderer.MeasureText(selectedText).X;

            var selectionRect = new LayoutRect(
                textPos.X + beforeWidth,
                textPos.Y,
                selectionWidth,
                ThemeManager.Current.LineHeight
            );
            renderer.DrawRectangle(selectionRect, SelectionColor);
        }

        // Draw text with syntax highlighting
        if (!string.IsNullOrEmpty(Text))
        {
            List<ColoredSegment> segments = SyntaxHighlighter.Highlight(Text);
            float currentX = textPos.X;

            foreach (ColoredSegment segment in segments)
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
            {
                _cursorBlinkTimer = 0;
            }

            if (_cursorBlinkTimer < CursorBlinkRate / 2)
            {
                string textBeforeCursor = Text.Substring(0, CursorPosition);
                float cursorX = textPos.X + renderer.MeasureText(textBeforeCursor).X;

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
    ///     Handles mouse click to position cursor at the clicked character.
    ///     Uses binary search for efficiency with long text.
    /// </summary>
    /// <param name="mousePos">Mouse position</param>
    /// <param name="renderer">UI renderer for text measurement</param>
    /// <param name="extendSelection">If true (Shift held), extends selection instead of clearing</param>
    private void HandleMouseClick(Point mousePos, UIRenderer renderer, bool extendSelection = false)
    {
        int newPosition;

        if (string.IsNullOrEmpty(Text))
        {
            newPosition = 0;
        }
        else
        {
            // Calculate text start position (after padding and prompt)
            float promptWidth = renderer.MeasureText(Prompt).X;
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
                float totalWidth = renderer.MeasureText(Text).X;
                if (relativeX >= totalWidth)
                {
                    newPosition = Text.Length;
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
            if (!HasSelection)
            {
                HasSelection = true;
                _selectionStart = CursorPosition;
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

        CursorPosition = newPosition;
    }

    protected override bool IsInteractive()
    {
        return true;
    }

    /// <summary>
    ///     Handles all mouse input for focus, cursor positioning, and selection.
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
    ///     Handles mouse drag to extend selection.
    /// </summary>
    private void HandleMouseDrag(Point mousePos, UIRenderer renderer)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        // Calculate new cursor position from mouse
        float promptWidth = renderer.MeasureText(Prompt).X;
        float textStartX = Rect.X + Padding + promptWidth;
        float relativeX = mousePos.X - textStartX;

        int newPosition;
        if (relativeX <= 0)
        {
            newPosition = 0;
        }
        else
        {
            float totalWidth = renderer.MeasureText(Text).X;
            if (relativeX >= totalWidth)
            {
                newPosition = Text.Length;
            }
            else
            {
                // Binary search for position
                newPosition = FindCharacterPosition(relativeX, renderer);
            }
        }

        // If this is the first drag movement, start selection
        if (!_isSelectingWithMouse && newPosition != CursorPosition)
        {
            _isSelectingWithMouse = true;
            if (!HasSelection)
            {
                HasSelection = true;
                _selectionStart = CursorPosition;
            }
        }

        // Update cursor and selection end
        if (_isSelectingWithMouse)
        {
            CursorPosition = newPosition;
            _selectionEnd = newPosition;
        }
    }

    /// <summary>
    ///     Binary search to find character position from X coordinate.
    /// </summary>
    private int FindCharacterPosition(float relativeX, UIRenderer renderer)
    {
        int left = 0;
        int right = Text.Length;

        while (left < right)
        {
            int mid = (left + right) / 2;
            string substring = Text.Substring(0, mid);
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
            string substringAtLeft = Text.Substring(0, left);
            string substringAtPrev = Text.Substring(0, left - 1);
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
            if (!IsMultiLine || !input.IsShiftDown())
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
            OnRequestCompletions?.Invoke(Text);
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
            CursorPosition = Math.Max(0, CursorPosition - 1);
            ClearSelection();
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.Right))
        {
            CursorPosition = Math.Min(Text.Length, CursorPosition + 1);
            ClearSelection();
            return;
        }

        // Home/End - with repeat
        if (input.IsKeyPressedWithRepeat(Keys.Home))
        {
            CursorPosition = 0;
            ClearSelection();
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.End))
        {
            CursorPosition = Text.Length;
            ClearSelection();
            return;
        }

        // Ctrl+A - Select all
        if (input.IsCtrlDown() && input.IsKeyPressed(Keys.A))
        {
            HasSelection = true;
            _selectionStart = 0;
            _selectionEnd = Text.Length;
            return;
        }

        // Ctrl+C - Copy
        if (input.IsCtrlDown() && input.IsKeyPressed(Keys.C))
        {
            if (HasSelection)
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
        foreach (Keys key in Enum.GetValues<Keys>())
        {
            if (input.IsKeyPressedWithRepeat(key))
            {
                char? ch = KeyboardHelper.KeyToChar(key, input.IsShiftDown());
                if (ch.HasValue)
                {
                    InsertText(ch.Value.ToString());
                }
            }
        }
    }
}
