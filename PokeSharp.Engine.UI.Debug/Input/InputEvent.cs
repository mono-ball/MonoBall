using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace PokeSharp.Engine.UI.Debug.Input;

/// <summary>
///     Represents a UI input event (mouse, keyboard, etc.).
/// </summary>
public abstract class InputEvent
{
    /// <summary>Whether this event has been handled (stops propagation)</summary>
    public bool Handled { get; set; }
}

/// <summary>
///     Mouse click event.
/// </summary>
public class MouseClickEvent : InputEvent
{
    public MouseButton Button { get; init; }
    public Point Position { get; init; }
    public int ClickCount { get; init; } = 1;
}

/// <summary>
///     Mouse move event.
/// </summary>
public class MouseMoveEvent : InputEvent
{
    public Point Position { get; init; }
    public Point Delta { get; init; }
}

/// <summary>
///     Mouse scroll event.
/// </summary>
public class MouseScrollEvent : InputEvent
{
    public int Delta { get; init; }
    public Point Position { get; init; }
}

/// <summary>
///     Key press event.
/// </summary>
public class KeyPressEvent : InputEvent
{
    public Keys Key { get; init; }
    public bool Ctrl { get; init; }
    public bool Shift { get; init; }
    public bool Alt { get; init; }
}

/// <summary>
///     Text input event (for typed characters).
/// </summary>
public class TextInputEvent : InputEvent
{
    public char Character { get; init; }
}
