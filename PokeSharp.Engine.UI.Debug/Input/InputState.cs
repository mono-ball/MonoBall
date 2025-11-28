using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace PokeSharp.Engine.UI.Debug.Input;

/// <summary>
///     Mouse button enumeration for simplified button handling.
/// </summary>
public enum MouseButton
{
    Left,
    Middle,
    Right,
}

/// <summary>
///     Captures per-frame input state for the UI system.
///     This is a snapshot of input at a specific moment in time.
/// </summary>
public class InputState
{
    // Input consumption tracking - prevents child components from processing already-handled input
    private readonly HashSet<Keys> _consumedKeys = new();
    private readonly HashSet<MouseButton> _consumedMouseButtons = new();

    // Key repeat tracking - provides smooth key repeat for held keys
    private readonly Dictionary<Keys, KeyRepeatState> _keyRepeatStates = new();
    private KeyboardState _currentKeyboard;
    private MouseState _currentMouse;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;

    /// <summary>
    ///     Creates a new input state snapshot.
    /// </summary>
    public InputState()
    {
        _currentKeyboard = Keyboard.GetState();
        _previousKeyboard = _currentKeyboard;
        _currentMouse = Mouse.GetState();
        _previousMouse = _currentMouse;
    }

    /// <summary>
    ///     Initial delay before key repeat starts (in seconds).
    /// </summary>
    public float InitialKeyRepeatDelay { get; set; } = 0.5f;

    /// <summary>
    ///     Interval between key repeats after initial delay (in seconds).
    /// </summary>
    public float KeyRepeatInterval { get; set; } = 0.05f; // 20 repeats per second

    /// <summary>
    ///     Mouse position in screen coordinates.
    /// </summary>
    public Point MousePosition => _currentMouse.Position;

    /// <summary>
    ///     Gets the current raw mouse state for debugging.
    /// </summary>
    public MouseState CurrentMouseState => _currentMouse;

    /// <summary>
    ///     Mouse scroll wheel delta this frame.
    /// </summary>
    public int ScrollWheelDelta => _currentMouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;

    /// <summary>
    ///     Game time for this frame (used for animations like cursor blinking).
    /// </summary>
    public GameTime GameTime { get; set; } = new();

    /// <summary>
    ///     Updates the input state with current input.
    /// </summary>
    public void Update()
    {
        _previousKeyboard = _currentKeyboard;
        _previousMouse = _currentMouse;
        _currentKeyboard = Keyboard.GetState();
        _currentMouse = Mouse.GetState();

        // Clear consumed input from previous frame
        _consumedKeys.Clear();
        _consumedMouseButtons.Clear();

        // Update key repeat states
        float deltaTime = (float)GameTime.ElapsedGameTime.TotalSeconds;
        var keysToRemove = new List<Keys>();

        foreach (KeyValuePair<Keys, KeyRepeatState> kvp in _keyRepeatStates)
        {
            Keys key = kvp.Key;
            KeyRepeatState state = kvp.Value;

            // If key is no longer held, mark for removal
            if (!_currentKeyboard.IsKeyDown(key))
            {
                keysToRemove.Add(key);
                continue;
            }

            // Update hold time
            state.HoldTime += deltaTime;

            // Update repeat timer if past initial delay
            if (state.HoldTime >= InitialKeyRepeatDelay)
            {
                state.TimeSinceLastRepeat += deltaTime;
            }
        }

        // Remove keys that are no longer held
        foreach (Keys key in keysToRemove)
        {
            _keyRepeatStates.Remove(key);
        }
    }

    /// <summary>
    ///     Creates a snapshot of the current input state.
    /// </summary>
    public static InputState Capture()
    {
        var state = new InputState();
        state.Update();
        return state;
    }

    // Mouse button methods

    /// <summary>
    ///     Checks if a mouse button is currently pressed.
    /// </summary>
    public bool IsMouseButtonDown(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => _currentMouse.LeftButton == ButtonState.Pressed,
            MouseButton.Middle => _currentMouse.MiddleButton == ButtonState.Pressed,
            MouseButton.Right => _currentMouse.RightButton == ButtonState.Pressed,
            _ => false,
        };
    }

    /// <summary>
    ///     Checks if a mouse button was just pressed this frame (and not consumed by another component).
    /// </summary>
    public bool IsMouseButtonPressed(MouseButton button)
    {
        if (_consumedMouseButtons.Contains(button))
        {
            return false;
        }

        ButtonState current = GetMouseButtonState(_currentMouse, button);
        ButtonState previous = GetMouseButtonState(_previousMouse, button);
        return current == ButtonState.Pressed && previous == ButtonState.Released;
    }

    /// <summary>
    ///     Checks if a mouse button was just released this frame.
    ///     Note: Does not check consumption - release events should be available to all components.
    ///     Components can only be hovered one at a time, so there's no risk of conflicts.
    /// </summary>
    public bool IsMouseButtonReleased(MouseButton button)
    {
        ButtonState current = GetMouseButtonState(_currentMouse, button);
        ButtonState previous = GetMouseButtonState(_previousMouse, button);
        return current == ButtonState.Released && previous == ButtonState.Pressed;
    }

    private static ButtonState GetMouseButtonState(MouseState state, MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => state.LeftButton,
            MouseButton.Middle => state.MiddleButton,
            MouseButton.Right => state.RightButton,
            _ => ButtonState.Released,
        };
    }

    // Keyboard methods

    /// <summary>
    ///     Checks if a key is currently pressed.
    /// </summary>
    public bool IsKeyDown(Keys key)
    {
        return _currentKeyboard.IsKeyDown(key);
    }

    /// <summary>
    ///     Checks if a key was just pressed this frame (and not consumed by another component).
    ///     Does NOT include key repeat - use IsKeyPressedWithRepeat for that.
    /// </summary>
    public bool IsKeyPressed(Keys key)
    {
        return !_consumedKeys.Contains(key)
            && _currentKeyboard.IsKeyDown(key)
            && _previousKeyboard.IsKeyUp(key);
    }

    /// <summary>
    ///     Checks if a key was just pressed OR is repeating (and not consumed).
    ///     This provides natural key repeat behavior like text editors.
    ///     Returns true on:
    ///     1. Initial press
    ///     2. After InitialKeyRepeatDelay, every KeyRepeatInterval
    /// </summary>
    public bool IsKeyPressedWithRepeat(Keys key)
    {
        if (_consumedKeys.Contains(key))
        {
            return false;
        }

        // Initial press
        if (_currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key))
        {
            // Start tracking this key for repeat
            _keyRepeatStates[key] = new KeyRepeatState { HoldTime = 0, TimeSinceLastRepeat = 0 };
            return true;
        }

        // Check for repeat
        if (_keyRepeatStates.TryGetValue(key, out KeyRepeatState? state))
        {
            // Past initial delay and time for another repeat?
            if (
                state.HoldTime >= InitialKeyRepeatDelay
                && state.TimeSinceLastRepeat >= KeyRepeatInterval
            )
            {
                state.TimeSinceLastRepeat = 0; // Reset repeat timer
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Checks if a key was just released this frame.
    /// </summary>
    public bool IsKeyReleased(Keys key)
    {
        return _currentKeyboard.IsKeyUp(key) && _previousKeyboard.IsKeyDown(key);
    }

    /// <summary>
    ///     Checks if Ctrl/Cmd is currently pressed.
    /// </summary>
    public bool IsCtrlDown()
    {
        return _currentKeyboard.IsKeyDown(Keys.LeftControl)
            || _currentKeyboard.IsKeyDown(Keys.RightControl);
    }

    /// <summary>
    ///     Checks if Shift is currently pressed.
    /// </summary>
    public bool IsShiftDown()
    {
        return _currentKeyboard.IsKeyDown(Keys.LeftShift)
            || _currentKeyboard.IsKeyDown(Keys.RightShift);
    }

    /// <summary>
    ///     Checks if Alt is currently pressed.
    /// </summary>
    public bool IsAltDown()
    {
        return _currentKeyboard.IsKeyDown(Keys.LeftAlt)
            || _currentKeyboard.IsKeyDown(Keys.RightAlt);
    }

    // Input consumption methods

    /// <summary>
    ///     Marks a key as consumed for this frame, preventing child components from processing it.
    ///     Should be called by parent components after they handle input.
    /// </summary>
    public void ConsumeKey(Keys key)
    {
        _consumedKeys.Add(key);
    }

    /// <summary>
    ///     Marks a mouse button as consumed for this frame, preventing child components from processing it.
    ///     Should be called by parent components after they handle input.
    /// </summary>
    public void ConsumeMouseButton(MouseButton button)
    {
        _consumedMouseButtons.Add(button);
    }

    /// <summary>
    ///     Checks if a key has been consumed this frame.
    /// </summary>
    public bool IsKeyConsumed(Keys key)
    {
        return _consumedKeys.Contains(key);
    }

    /// <summary>
    ///     Checks if a mouse button has been consumed this frame.
    /// </summary>
    public bool IsMouseButtonConsumed(MouseButton button)
    {
        return _consumedMouseButtons.Contains(button);
    }

    /// <summary>
    ///     Checks if any mouse button state changed this frame.
    ///     Useful for optimization - skip hover updates if mouse is idle.
    /// </summary>
    public bool AnyMouseButtonsChanged()
    {
        return _currentMouse.LeftButton != _previousMouse.LeftButton
            || _currentMouse.MiddleButton != _previousMouse.MiddleButton
            || _currentMouse.RightButton != _previousMouse.RightButton;
    }

    /// <summary>
    ///     Resets the repeat state for a specific key.
    ///     Useful when you want to cancel repeat (e.g., when closing a menu).
    /// </summary>
    public void ResetKeyRepeat(Keys key)
    {
        _keyRepeatStates.Remove(key);
    }

    /// <summary>
    ///     Resets all key repeat states.
    ///     Useful when changing focus or closing UI elements.
    /// </summary>
    public void ResetAllKeyRepeats()
    {
        _keyRepeatStates.Clear();
    }
}

/// <summary>
///     Tracks the repeat state of a held key.
/// </summary>
internal class KeyRepeatState
{
    /// <summary>
    ///     How long the key has been held (in seconds).
    /// </summary>
    public float HoldTime { get; set; }

    /// <summary>
    ///     Time since the last repeat fired (in seconds).
    /// </summary>
    public float TimeSinceLastRepeat { get; set; }
}
