using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
///     Manages cursor state for a text editor including position, blinking animation, and movement.
/// </summary>
public class TextEditorCursor
{
    /// <summary>
    ///     Gets or sets the blink rate in seconds.
    /// </summary>
    private float? _blinkRate;

    private float _blinkTimer;
    private int _column;
    private int _line;

    /// <summary>
    ///     Gets or sets the cursor line (0-based).
    /// </summary>
    public int Line
    {
        get => _line;
        set => _line = Math.Max(0, value);
    }

    /// <summary>
    ///     Gets or sets the cursor column (0-based).
    /// </summary>
    public int Column
    {
        get => _column;
        set => _column = Math.Max(0, value);
    }

    public float BlinkRate
    {
        get => _blinkRate ?? ThemeManager.Current.CursorBlinkRate;
        set => _blinkRate = value;
    }

    /// <summary>
    ///     Gets whether the cursor should be visible based on blink animation.
    /// </summary>
    public bool IsVisible => _blinkTimer < BlinkRate;

    /// <summary>
    ///     Updates the cursor blink animation.
    /// </summary>
    public void Update(GameTime gameTime)
    {
        _blinkTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_blinkTimer >= BlinkRate * 2)
        {
            _blinkTimer = 0;
        }
    }

    /// <summary>
    ///     Resets the blink animation (makes cursor visible immediately).
    ///     Call this when cursor moves or text changes.
    /// </summary>
    public void ResetBlink()
    {
        _blinkTimer = 0;
    }

    /// <summary>
    ///     Sets the cursor position.
    /// </summary>
    public void SetPosition(int line, int column)
    {
        Line = line;
        Column = column;
        ResetBlink();
    }

    /// <summary>
    ///     Moves cursor to start of line.
    /// </summary>
    public void MoveToLineStart()
    {
        Column = 0;
        ResetBlink();
    }

    /// <summary>
    ///     Moves cursor to end of line.
    /// </summary>
    public void MoveToLineEnd(int lineLength)
    {
        Column = lineLength;
        ResetBlink();
    }

    /// <summary>
    ///     Moves cursor one character left, with optional line wrap.
    /// </summary>
    public bool MoveLeft(Func<int, int>? getLineLength = null)
    {
        if (Column > 0)
        {
            Column--;
            ResetBlink();
            return true;
        }

        if (Line > 0 && getLineLength != null)
        {
            // Move to end of previous line
            Line--;
            Column = getLineLength(Line);
            ResetBlink();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Moves cursor one character right, with optional line wrap.
    /// </summary>
    public bool MoveRight(int currentLineLength, Func<int>? getTotalLines = null)
    {
        if (Column < currentLineLength)
        {
            Column++;
            ResetBlink();
            return true;
        }

        if (getTotalLines != null && Line < getTotalLines() - 1)
        {
            // Move to start of next line
            Line++;
            Column = 0;
            ResetBlink();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Moves cursor up one line, maintaining column position if possible.
    /// </summary>
    public bool MoveUp(Func<int, int> getLineLength)
    {
        if (Line > 0)
        {
            Line--;
            Column = Math.Min(Column, getLineLength(Line));
            ResetBlink();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Moves cursor down one line, maintaining column position if possible.
    /// </summary>
    public bool MoveDown(int totalLines, Func<int, int> getLineLength)
    {
        if (Line < totalLines - 1)
        {
            Line++;
            Column = Math.Min(Column, getLineLength(Line));
            ResetBlink();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Clamps cursor position to valid range.
    /// </summary>
    public void ClampToValid(int totalLines, Func<int, int> getLineLength)
    {
        Line = Math.Clamp(Line, 0, totalLines - 1);
        Column = Math.Clamp(Column, 0, getLineLength(Line));
    }
}
