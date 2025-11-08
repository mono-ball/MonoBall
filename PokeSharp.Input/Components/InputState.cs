using PokeSharp.Core.Components.Movement;

namespace PokeSharp.Input.Components;

/// <summary>
///     Component tracking input state and buffering for responsive controls.
/// </summary>
public struct InputState
{
    /// <summary>
    ///     Gets or sets the currently pressed direction.
    /// </summary>
    public Direction PressedDirection { get; set; }

    /// <summary>
    ///     Gets or sets whether the action button is pressed.
    /// </summary>
    public bool ActionPressed { get; set; }

    /// <summary>
    ///     Gets or sets the remaining time for input buffering in seconds.
    /// </summary>
    public float InputBufferTime { get; set; }

    /// <summary>
    ///     Gets or sets whether input is currently enabled.
    /// </summary>
    public bool InputEnabled { get; set; }

    /// <summary>
    ///     Initializes a new instance of the InputState struct.
    /// </summary>
    public InputState()
    {
        PressedDirection = Direction.None;
        ActionPressed = false;
        InputBufferTime = 0f;
        InputEnabled = true;
    }
}
