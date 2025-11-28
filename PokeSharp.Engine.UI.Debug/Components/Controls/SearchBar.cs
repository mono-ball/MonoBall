using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
/// Search bar component for finding text in output.
/// Shows search input, match count, and navigation buttons.
/// </summary>
public class SearchBar : UIComponent
{
    private string _searchText = string.Empty;
    private int _cursorPosition = 0;
    private float _cursorBlinkTimer = 0;
    private static float CursorBlinkRate => ThemeManager.Current.CursorBlinkRate;

    // Visual properties - nullable for theme fallback
    private Color? _backgroundColor;
    private Color? _textColor;
    private Color? _cursorColor;
    private Color? _borderColor;
    private Color? _focusBorderColor;
    private Color? _infoColor;

    public Color BackgroundColor { get => _backgroundColor ?? ThemeManager.Current.ConsoleSearchBackground; set => _backgroundColor = value; }
    public Color TextColor { get => _textColor ?? ThemeManager.Current.InputText; set => _textColor = value; }
    public Color CursorColor { get => _cursorColor ?? ThemeManager.Current.InputCursor; set => _cursorColor = value; }
    public Color BorderColor { get => _borderColor ?? ThemeManager.Current.BorderPrimary; set => _borderColor = value; }
    public Color FocusBorderColor { get => _focusBorderColor ?? ThemeManager.Current.BorderFocus; set => _focusBorderColor = value; }
    public Color InfoColor { get => _infoColor ?? ThemeManager.Current.TextSecondary; set => _infoColor = value; }
    public float Padding { get; set; } = 8f;
    public float BorderThickness { get; set; } = 1f;

    // Search state
    public int TotalMatches { get; set; } = 0;
    public int CurrentMatchIndex { get; set; } = 0;

    // Events
    public Action<string>? OnSearchTextChanged { get; set; }
    public Action? OnNextMatch { get; set; }
    public Action? OnPreviousMatch { get; set; }
    public Action? OnClose { get; set; }

    public SearchBar(string id)
    {
        Id = id;
    }

    public string SearchText => _searchText;

    public void SetSearchText(string text)
    {
        _searchText = text ?? string.Empty;
        _cursorPosition = Math.Clamp(_cursorPosition, 0, _searchText.Length);
    }

    public void Clear()
    {
        _searchText = string.Empty;
        _cursorPosition = 0;
        TotalMatches = 0;
        CurrentMatchIndex = 0;
    }

    protected override void OnRender(UIContext context)
    {
        // Don't render if height is 0 (hidden)
        if (Rect.Height <= 0)
            return;

        var renderer = Renderer;
        var resolvedRect = Rect;

        // Draw background
        renderer.DrawRectangle(resolvedRect, BackgroundColor);

        // Draw border (use focus color when focused)
        var borderColor = IsFocused() ? FocusBorderColor : BorderColor;
        renderer.DrawRectangleOutline(resolvedRect, borderColor, (int)BorderThickness);

        // Handle input if focused
        if (IsFocused())
        {
            HandleInput(context.Input);
        }

        // Calculate layout
        var contentX = resolvedRect.X + Padding;
        var contentY = resolvedRect.Y + Padding;
        var contentHeight = resolvedRect.Height - Padding * 2;

        // Draw label
        var labelText = "Find: ";
        renderer.DrawText(labelText, new Vector2(contentX, contentY), InfoColor);
        var labelWidth = renderer.MeasureText(labelText).X;

        // Draw search text
        var textX = contentX + labelWidth;
        if (!string.IsNullOrEmpty(_searchText))
        {
            renderer.DrawText(_searchText, new Vector2(textX, contentY), TextColor);
        }

        // Draw cursor if focused
        if (IsFocused())
        {
            _cursorBlinkTimer += (float)context.Input.GameTime.ElapsedGameTime.TotalSeconds;
            if (_cursorBlinkTimer > CursorBlinkRate)
                _cursorBlinkTimer = 0;

            if (_cursorBlinkTimer < CursorBlinkRate / 2)
            {
                var textBeforeCursor = _searchText.Substring(0, _cursorPosition);
                var cursorX = textX + renderer.MeasureText(textBeforeCursor).X;
                var cursorRect = new LayoutRect(cursorX, contentY, 2, contentHeight);
                renderer.DrawRectangle(cursorRect, CursorColor);
            }
        }

        // Draw match count on the right
        if (TotalMatches > 0)
        {
            var matchText = $"{CurrentMatchIndex + 1}/{TotalMatches}";
            var matchWidth = renderer.MeasureText(matchText).X;
            var matchX = resolvedRect.Right - Padding - matchWidth;
            renderer.DrawText(matchText, new Vector2(matchX, contentY), InfoColor);
        }
        else if (!string.IsNullOrEmpty(_searchText))
        {
            var noMatchText = "No matches";
            var noMatchWidth = renderer.MeasureText(noMatchText).X;
            var noMatchX = resolvedRect.Right - Padding - noMatchWidth;
            renderer.DrawText(noMatchText, new Vector2(noMatchX, contentY), InfoColor);
        }
    }

    private void HandleInput(InputState input)
    {
        // Escape - Close search
        if (input.IsKeyPressed(Keys.Escape))
        {
            OnClose?.Invoke();
            input.ConsumeKey(Keys.Escape);
            return;
        }

        // Enter or F3 - Next match
        if (input.IsKeyPressed(Keys.Enter) || input.IsKeyPressed(Keys.F3))
        {
            if (input.IsShiftDown())
            {
                // Shift+Enter or Shift+F3 - Previous match
                OnPreviousMatch?.Invoke();
            }
            else
            {
                // Regular Enter or F3 - Next match
                OnNextMatch?.Invoke();
            }
            return;
        }

        // Backspace
        if (input.IsKeyPressedWithRepeat(Keys.Back))
        {
            if (_cursorPosition > 0)
            {
                _searchText = _searchText.Remove(_cursorPosition - 1, 1);
                _cursorPosition--;
                OnSearchTextChanged?.Invoke(_searchText);
            }
            return;
        }

        // Delete
        if (input.IsKeyPressedWithRepeat(Keys.Delete))
        {
            if (_cursorPosition < _searchText.Length)
            {
                _searchText = _searchText.Remove(_cursorPosition, 1);
                OnSearchTextChanged?.Invoke(_searchText);
            }
            return;
        }

        // Arrow keys
        if (input.IsKeyPressedWithRepeat(Keys.Left))
        {
            if (_cursorPosition > 0)
                _cursorPosition--;
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.Right))
        {
            if (_cursorPosition < _searchText.Length)
                _cursorPosition++;
            return;
        }

        // Home/End
        if (input.IsKeyPressed(Keys.Home))
        {
            _cursorPosition = 0;
            return;
        }

        if (input.IsKeyPressed(Keys.End))
        {
            _cursorPosition = _searchText.Length;
            return;
        }

        // Character input
        foreach (var key in Enum.GetValues<Keys>())
        {
            if (input.IsKeyPressedWithRepeat(key))
            {
                var ch = Utilities.KeyboardHelper.KeyToChar(key, input.IsShiftDown());
                if (ch.HasValue)
                {
                    _searchText = _searchText.Insert(_cursorPosition, ch.Value.ToString());
                    _cursorPosition++;
                    OnSearchTextChanged?.Invoke(_searchText);
                }
            }
        }
    }

    protected override bool IsInteractive() => true;
}

