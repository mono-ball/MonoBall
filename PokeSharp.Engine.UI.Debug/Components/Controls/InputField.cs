using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
///     A text input field component.
/// </summary>
public class InputField : UIComponent
{
    private float _cursorBlinkTimer;

    /// <summary>Current text value</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Placeholder text when empty</summary>
    public string Placeholder { get; set; } = string.Empty;

    /// <summary>Cursor position</summary>
    public int CursorPosition { get; set; }

    /// <summary>Callback when text changes</summary>
    public Action<string>? OnTextChanged { get; set; }

    /// <summary>Callback when Enter is pressed</summary>
    public Action<string>? OnSubmit { get; set; }

    protected override bool IsInteractive()
    {
        return Enabled;
    }

    protected override void OnRender(UIContext context)
    {
        bool isFocused = IsFocused();

        // Draw background
        context.Renderer.DrawRectangle(Rect, Theme.InputBackground);

        // Draw border (highlight when focused)
        Color borderColor = isFocused ? Theme.BorderFocus : Theme.BorderPrimary;
        context.Renderer.DrawRectangleOutline(Rect, borderColor, Theme.BorderWidth);

        // Draw text or placeholder
        float textX = Rect.X + Theme.PaddingMedium;
        float textY = Rect.Y + ((Rect.Height - context.Renderer.GetLineHeight()) / 2);

        if (string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Placeholder) && !isFocused)
        {
            context.Renderer.DrawText(Placeholder, textX, textY, Theme.TextDim);
        }
        else if (!string.IsNullOrEmpty(Text))
        {
            context.Renderer.DrawText(Text, textX, textY, Theme.InputText);
        }

        // Draw cursor when focused
        if (isFocused)
        {
            _cursorBlinkTimer += 0.016f; // Approximate frame time
            bool showCursor = _cursorBlinkTimer % 1.0f < 0.5f;

            if (showCursor)
            {
                string textBeforeCursor = Text.Substring(0, Math.Min(CursorPosition, Text.Length));
                float cursorX = textX + context.Renderer.MeasureText(textBeforeCursor).X;

                var cursorRect = new LayoutRect(
                    cursorX,
                    textY,
                    2,
                    context.Renderer.GetLineHeight()
                );
                context.Renderer.DrawRectangle(cursorRect, Theme.InputCursor);
            }
        }

        // Handle input when focused
        if (isFocused)
        {
            HandleInput(context);
        }

        // Handle click to focus
        // Handle focus on mouse RELEASE
        if (IsHovered() && context.Input.IsMouseButtonReleased(MouseButton.Left))
        {
            context.SetFocus(Id);
        }
    }

    private void HandleInput(UIContext context)
    {
        InputState input = context.Input;

        // Handle backspace
        if (input.IsKeyPressed(Keys.Back) && CursorPosition > 0 && Text.Length > 0)
        {
            Text = Text.Remove(CursorPosition - 1, 1);
            CursorPosition--;
            OnTextChanged?.Invoke(Text);
        }

        // Handle delete
        if (input.IsKeyPressed(Keys.Delete) && CursorPosition < Text.Length)
        {
            Text = Text.Remove(CursorPosition, 1);
            OnTextChanged?.Invoke(Text);
        }

        // Handle arrow keys
        if (input.IsKeyPressed(Keys.Left) && CursorPosition > 0)
        {
            CursorPosition--;
        }

        if (input.IsKeyPressed(Keys.Right) && CursorPosition < Text.Length)
        {
            CursorPosition++;
        }

        // Handle Home/End
        if (input.IsKeyPressed(Keys.Home))
        {
            CursorPosition = 0;
        }

        if (input.IsKeyPressed(Keys.End))
        {
            CursorPosition = Text.Length;
        }

        // Handle Enter (submit)
        if (input.IsKeyPressed(Keys.Enter))
        {
            OnSubmit?.Invoke(Text);
        }

        // Handle Escape (clear focus)
        if (input.IsKeyPressed(Keys.Escape))
        {
            context.ClearFocus();
        }

        // Note: Character input would require TextInput events from MonoGame
        // For now, this is a simplified implementation
    }
}
