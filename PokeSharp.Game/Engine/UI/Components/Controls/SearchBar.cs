using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Game.Engine.UI.Debug.Components.Base;
using PokeSharp.Game.Engine.UI.Debug.Core;
using PokeSharp.Game.Engine.UI.Debug.Input;
using PokeSharp.Game.Engine.UI.Debug.Layout;
using PokeSharp.Game.Engine.UI.Debug.Utilities;

namespace PokeSharp.Game.Engine.UI.Debug.Components.Controls;

/// <summary>
///     Search bar component for finding text in output.
///     Shows search input, match count, and navigation buttons.
/// </summary>
public class SearchBar : UIComponent
{
    // Visual properties - nullable for theme fallback
    private Color? _backgroundColor;
    private Color? _borderColor;
    private float _cursorBlinkTimer;
    private Color? _cursorColor;
    private int _cursorPosition;
    private Color? _focusBorderColor;
    private Color? _infoColor;
    private Color? _textColor;

    public SearchBar(string id)
    {
        Id = id;
    }

    private static float CursorBlinkRate => ThemeManager.Current.CursorBlinkRate;

    public Color BackgroundColor
    {
        get => _backgroundColor ?? ThemeManager.Current.ConsoleSearchBackground;
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

    public Color InfoColor
    {
        get => _infoColor ?? ThemeManager.Current.TextSecondary;
        set => _infoColor = value;
    }

    public float Padding { get; set; } = 8f;
    public float BorderThickness { get; set; } = 1f;

    // Search state
    public int TotalMatches { get; set; }
    public int CurrentMatchIndex { get; set; }

    // Events
    public Action<string>? OnSearchTextChanged { get; set; }
    public Action? OnNextMatch { get; set; }
    public Action? OnPreviousMatch { get; set; }
    public Action? OnClose { get; set; }

    public string SearchText { get; private set; } = string.Empty;

    public void SetSearchText(string text)
    {
        SearchText = text ?? string.Empty;
        _cursorPosition = Math.Clamp(_cursorPosition, 0, SearchText.Length);
    }

    public void Clear()
    {
        SearchText = string.Empty;
        _cursorPosition = 0;
        TotalMatches = 0;
        CurrentMatchIndex = 0;
    }

    protected override void OnRender(UIContext context)
    {
        // Don't render if height is 0 (hidden)
        if (Rect.Height <= 0)
        {
            return;
        }

        UIRenderer renderer = Renderer;
        LayoutRect resolvedRect = Rect;

        // Draw background
        renderer.DrawRectangle(resolvedRect, BackgroundColor);

        // Draw border (use focus color when focused)
        Color borderColor = IsFocused() ? FocusBorderColor : BorderColor;
        renderer.DrawRectangleOutline(resolvedRect, borderColor, (int)BorderThickness);

        // Handle input if focused
        if (IsFocused())
        {
            HandleInput(context.Input);
        }

        // Calculate layout
        float contentX = resolvedRect.X + Padding;
        float contentY = resolvedRect.Y + Padding;
        float contentHeight = resolvedRect.Height - (Padding * 2);

        // Draw label
        string labelText = "Find: ";
        renderer.DrawText(labelText, new Vector2(contentX, contentY), InfoColor);
        float labelWidth = renderer.MeasureText(labelText).X;

        // Draw search text
        float textX = contentX + labelWidth;
        if (!string.IsNullOrEmpty(SearchText))
        {
            renderer.DrawText(SearchText, new Vector2(textX, contentY), TextColor);
        }

        // Draw cursor if focused
        if (IsFocused())
        {
            _cursorBlinkTimer += (float)context.Input.GameTime.ElapsedGameTime.TotalSeconds;
            if (_cursorBlinkTimer > CursorBlinkRate)
            {
                _cursorBlinkTimer = 0;
            }

            if (_cursorBlinkTimer < CursorBlinkRate / 2)
            {
                string textBeforeCursor = SearchText.Substring(0, _cursorPosition);
                float cursorX = textX + renderer.MeasureText(textBeforeCursor).X;
                var cursorRect = new LayoutRect(cursorX, contentY, 2, contentHeight);
                renderer.DrawRectangle(cursorRect, CursorColor);
            }
        }

        // Draw match count on the right
        if (TotalMatches > 0)
        {
            string matchText = $"{CurrentMatchIndex + 1}/{TotalMatches}";
            float matchWidth = renderer.MeasureText(matchText).X;
            float matchX = resolvedRect.Right - Padding - matchWidth;
            renderer.DrawText(matchText, new Vector2(matchX, contentY), InfoColor);
        }
        else if (!string.IsNullOrEmpty(SearchText))
        {
            string noMatchText = "No matches";
            float noMatchWidth = renderer.MeasureText(noMatchText).X;
            float noMatchX = resolvedRect.Right - Padding - noMatchWidth;
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
                SearchText = SearchText.Remove(_cursorPosition - 1, 1);
                _cursorPosition--;
                OnSearchTextChanged?.Invoke(SearchText);
            }

            return;
        }

        // Delete
        if (input.IsKeyPressedWithRepeat(Keys.Delete))
        {
            if (_cursorPosition < SearchText.Length)
            {
                SearchText = SearchText.Remove(_cursorPosition, 1);
                OnSearchTextChanged?.Invoke(SearchText);
            }

            return;
        }

        // Arrow keys
        if (input.IsKeyPressedWithRepeat(Keys.Left))
        {
            if (_cursorPosition > 0)
            {
                _cursorPosition--;
            }

            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.Right))
        {
            if (_cursorPosition < SearchText.Length)
            {
                _cursorPosition++;
            }

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
            _cursorPosition = SearchText.Length;
            return;
        }

        // Character input
        foreach (Keys key in Enum.GetValues<Keys>())
        {
            if (input.IsKeyPressedWithRepeat(key))
            {
                char? ch = KeyboardHelper.KeyToChar(key, input.IsShiftDown());
                if (ch.HasValue)
                {
                    SearchText = SearchText.Insert(_cursorPosition, ch.Value.ToString());
                    _cursorPosition++;
                    OnSearchTextChanged?.Invoke(SearchText);
                }
            }
        }
    }

    protected override bool IsInteractive()
    {
        return true;
    }
}
