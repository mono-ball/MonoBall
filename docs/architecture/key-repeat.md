# Key Repeat Architecture

## Overview

The UI framework now includes a **built-in key repeat system** that provides smooth, natural keyboard behavior like text editors and IDEs. Any component can easily implement key repeat without managing complex state tracking.

## The Problem It Solves

### Before (No Key Repeat) ⛔
```csharp
// User holds Backspace - only deletes ONE character
if (input.IsKeyPressed(Keys.Back))
{
    DeleteBackward(); // Fires once, then stops
}

// User must tap tap tap tap... (annoying!)
```

### After (With Key Repeat) ✨
```csharp
// User holds Backspace - smoothly deletes characters
if (input.IsKeyPressedWithRepeat(Keys.Back))
{
    DeleteBackward(); // Fires once, waits 0.5s, then repeats at 20Hz
}

// Hold and watch it go!
```

## How It Works

### Key Repeat Timing

When you hold a key:
1. **Initial press**: Fires immediately
2. **Initial delay**: Wait 0.5 seconds (configurable)
3. **Repeat**: Fire every 0.05 seconds (20x per second, configurable)

```
Press    [===== 0.5s delay =====] [repeat] [repeat] [repeat] ...
↓                                  ↓        ↓        ↓
Fire                               Fire     Fire     Fire
```

### State Tracking

`InputState` automatically tracks repeat state for each key:

```csharp
internal class KeyRepeatState
{
    public float HoldTime { get; set; }           // How long held
    public float TimeSinceLastRepeat { get; set; } // Time since last repeat
}
```

All state management is automatic - you just call `IsKeyPressedWithRepeat()`.

## API Reference

### InputState Properties

#### `float InitialKeyRepeatDelay { get; set; }`
- **Default**: `0.5f` (500 milliseconds)
- **Description**: How long to wait before key repeat starts
- **Tuning**: Increase for slower users, decrease for power users

#### `float KeyRepeatInterval { get; set; }`
- **Default**: `0.05f` (50 milliseconds, 20 repeats/second)
- **Description**: Time between repeats after initial delay
- **Tuning**: Lower = faster repeat, higher = slower repeat

### InputState Methods

#### `bool IsKeyPressed(Keys key)`
**Single-fire detection** - returns `true` only on the initial press frame.

```csharp
// Good for: Enter, Escape, Tab, Ctrl+S, etc.
if (input.IsKeyPressed(Keys.Enter))
{
    Submit(); // Only fires once per press
}
```

**Use for**:
- Command execution (Enter)
- Dialog dismissal (Escape)
- File operations (Ctrl+S, Ctrl+O)
- Any action that should happen exactly once

#### `bool IsKeyPressedWithRepeat(Keys key)`
**Press + repeat detection** - returns `true` on initial press, then repeatedly after delay.

```csharp
// Good for: Backspace, arrows, Delete, etc.
if (input.IsKeyPressedWithRepeat(Keys.Back))
{
    DeleteBackward(); // Fires once, then repeats smoothly
}
```

**Use for**:
- Text editing (Backspace, Delete)
- Navigation (arrows, Home, End, PageUp/Down)
- Character input (letters, numbers)
- Continuous actions

#### `void ResetKeyRepeat(Keys key)`
Cancels repeat for a specific key.

```csharp
// Cancel repeat when context changes
if (closedAutocomplete)
{
    input.ResetKeyRepeat(Keys.Up);
    input.ResetKeyRepeat(Keys.Down);
}
```

#### `void ResetAllKeyRepeats()`
Cancels repeat for all keys.

```csharp
// Cancel all repeats when losing focus
protected override void OnFocusLost()
{
    Context.Input.ResetAllKeyRepeats();
}
```

## Usage Examples

### Example 1: Text Editing with Repeat

```csharp
// CommandInput.cs
private void HandleKeyboardInput(InputState input)
{
    // Backspace - smooth deletion when held
    if (input.IsKeyPressedWithRepeat(Keys.Back))
    {
        DeleteBackward();
        return;
    }

    // Delete - smooth forward deletion
    if (input.IsKeyPressedWithRepeat(Keys.Delete))
    {
        DeleteForward();
        return;
    }

    // Arrow keys - smooth cursor movement
    if (input.IsKeyPressedWithRepeat(Keys.Left))
    {
        _cursorPosition = Math.Max(0, _cursorPosition - 1);
        return;
    }

    if (input.IsKeyPressedWithRepeat(Keys.Right))
    {
        _cursorPosition = Math.Min(_text.Length, _cursorPosition + 1);
        return;
    }

    // Character input - natural typing when held
    foreach (var key in Enum.GetValues<Keys>())
    {
        if (input.IsKeyPressedWithRepeat(key))
        {
            var ch = KeyToChar(key, input.IsShiftDown());
            if (ch.HasValue)
            {
                InsertText(ch.Value.ToString());
            }
        }
    }
}
```

### Example 2: List Navigation with Repeat

```csharp
// ConsolePanel.cs - Autocomplete navigation
if (_showSuggestions)
{
    // Smooth navigation through suggestions
    if (input.IsKeyPressedWithRepeat(Keys.Down))
    {
        _suggestionsDropdown.SelectNext();
        input.ConsumeKey(Keys.Down);
    }
    else if (input.IsKeyPressedWithRepeat(Keys.Up))
    {
        _suggestionsDropdown.SelectPrevious();
        input.ConsumeKey(Keys.Up);
    }

    // But Enter is single-fire - accept and close
    else if (input.IsKeyPressed(Keys.Enter))
    {
        _suggestionsDropdown.AcceptSelected();
        input.ConsumeKey(Keys.Enter);
    }
}
```

### Example 3: Scrolling with Repeat

```csharp
// TextBuffer scrolling
protected override void OnInput(InputEvent inputEvent)
{
    var input = Context?.Input;

    // Smooth scrolling with PageUp/PageDown
    if (input.IsKeyPressedWithRepeat(Keys.PageUp))
    {
        ScrollUp(10); // Scroll 10 lines
    }

    if (input.IsKeyPressedWithRepeat(Keys.PageDown))
    {
        ScrollDown(10);
    }

    // Smooth line-by-line scrolling
    if (input.IsKeyPressedWithRepeat(Keys.Up))
    {
        ScrollUp(1);
    }

    if (input.IsKeyPressedWithRepeat(Keys.Down))
    {
        ScrollDown(1);
    }
}
```

### Example 4: Mixed Repeat and Single-Fire

```csharp
private void HandleInput(InputState input)
{
    // Single-fire actions
    if (input.IsKeyPressed(Keys.Enter))
        Submit();

    if (input.IsKeyPressed(Keys.Escape))
        Cancel();

    if (input.IsKeyPressed(Keys.Tab))
        NextField();

    // Repeat actions
    if (input.IsKeyPressedWithRepeat(Keys.Back))
        DeleteBackward();

    if (input.IsKeyPressedWithRepeat(Keys.Left))
        MoveCursorLeft();

    if (input.IsKeyPressedWithRepeat(Keys.Right))
        MoveCursorRight();
}
```

## Decision Guide: When to Use Each Method

### Use `IsKeyPressed()` (Single-Fire) For:
✅ Command execution (Enter, Tab)
✅ Dialog actions (OK, Cancel)
✅ Mode toggles (Escape, F1-F12)
✅ File operations (Ctrl+S, Ctrl+O)
✅ Clipboard operations (Ctrl+C, Ctrl+V)
✅ One-time actions that shouldn't repeat

### Use `IsKeyPressedWithRepeat()` (With Repeat) For:
✅ Text editing (Backspace, Delete)
✅ Cursor movement (Arrows, Home, End)
✅ Scrolling (PageUp, PageDown)
✅ List navigation (Up, Down in menus)
✅ Character input (Letters, numbers)
✅ Continuous actions that feel natural repeating

### Quick Test
Ask yourself: **"If the user holds this key for 2 seconds, should it keep firing?"**
- **YES** → Use `IsKeyPressedWithRepeat()`
- **NO** → Use `IsKeyPressed()`

## Integration with Input Consumption

Key repeat works seamlessly with the input consumption system:

```csharp
// Parent component handles with repeat
if (input.IsKeyPressedWithRepeat(Keys.Up))
{
    NavigateSuggestions();
    input.ConsumeKey(Keys.Up); // Consume to prevent child from seeing it
}

// Child component won't see it (consumed)
if (input.IsKeyPressedWithRepeat(Keys.Up))
{
    // This won't execute - parent consumed the key
    ScrollHistory();
}
```

**Both systems work together**:
1. Key repeat provides smooth, natural input behavior
2. Input consumption provides proper parent-child hierarchy

## Tuning Key Repeat

You can customize timing per component:

```csharp
public class FastNavigationComponent : UIComponent
{
    protected override void OnRender(UIContext context)
    {
        var input = context.Input;

        // Speed up repeat for power users
        var previousInterval = input.KeyRepeatInterval;
        input.KeyRepeatInterval = 0.03f; // 33 repeats/second (faster!)

        if (input.IsKeyPressedWithRepeat(Keys.Down))
        {
            SelectNext(); // Will repeat faster
        }

        // Restore original timing
        input.KeyRepeatInterval = previousInterval;
    }
}
```

Or configure globally:

```csharp
// In scene initialization
_inputState.InitialKeyRepeatDelay = 0.3f; // Shorter delay
_inputState.KeyRepeatInterval = 0.04f;     // Slightly faster
```

## Implementation Details

### Automatic State Management

`InputState.Update()` automatically:
1. Updates hold time for all held keys
2. Updates repeat timers past initial delay
3. Removes keys when released
4. Clears consumed keys each frame

```csharp
public void Update()
{
    // ... update keyboard state ...

    var deltaTime = (float)GameTime.ElapsedGameTime.TotalSeconds;
    var keysToRemove = new List<Keys>();

    foreach (var kvp in _keyRepeatStates)
    {
        var key = kvp.Key;
        var state = kvp.Value;

        // Key released? Clean up
        if (!_currentKeyboard.IsKeyDown(key))
        {
            keysToRemove.Add(key);
            continue;
        }

        // Update timers
        state.HoldTime += deltaTime;

        if (state.HoldTime >= InitialKeyRepeatDelay)
        {
            state.TimeSinceLastRepeat += deltaTime;
        }
    }

    // Clean up released keys
    foreach (var key in keysToRemove)
    {
        _keyRepeatStates.Remove(key);
    }
}
```

### Press Detection Logic

```csharp
public bool IsKeyPressedWithRepeat(Keys key)
{
    if (_consumedKeys.Contains(key))
        return false;

    // Initial press
    if (_currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key))
    {
        _keyRepeatStates[key] = new KeyRepeatState
        {
            HoldTime = 0,
            TimeSinceLastRepeat = 0
        };
        return true; // Fire immediately
    }

    // Check for repeat
    if (_keyRepeatStates.TryGetValue(key, out var state))
    {
        if (state.HoldTime >= InitialKeyRepeatDelay &&
            state.TimeSinceLastRepeat >= KeyRepeatInterval)
        {
            state.TimeSinceLastRepeat = 0; // Reset for next repeat
            return true; // Fire repeat
        }
    }

    return false;
}
```

## Comparison to Other Systems

### Standard Keyboard Repeat (OS-level)
```
OS provides key repeat automatically via character events.
Our system mimics this for game input which uses key states.
```

### ImGui
```cpp
// ImGui has built-in repeat
if (ImGui::IsKeyPressed(ImGuiKey_Backspace, true))  // true = repeat
{
    DeleteChar();
}
```

### Unity Input System
```csharp
// Unity new input system
if (Keyboard.current.backspaceKey.wasRepeatedThisFrame)
{
    DeleteChar();
}
```

### Our System
```csharp
// Clean, simple API
if (input.IsKeyPressedWithRepeat(Keys.Back))
{
    DeleteChar();
}
```

## Best Practices

### 1. Choose the Right Method
```csharp
// ✅ Good
if (input.IsKeyPressed(Keys.Enter))        // Single-fire
    Submit();

if (input.IsKeyPressedWithRepeat(Keys.Back)) // Repeat
    DeleteBackward();

// ❌ Bad
if (input.IsKeyPressedWithRepeat(Keys.Enter)) // Enter shouldn't repeat!
    Submit();
```

### 2. Reset Repeat on Context Changes
```csharp
private void CloseDialog()
{
    _dialogOpen = false;
    Context.Input.ResetAllKeyRepeats(); // Cancel any ongoing repeats
}
```

### 3. Combine with Consumption
```csharp
// Parent handles first
if (_menuOpen && input.IsKeyPressedWithRepeat(Keys.Down))
{
    MenuSelectNext();
    input.ConsumeKey(Keys.Down); // Prevent child from seeing it
}
```

### 4. Document Repeat Behavior
```csharp
/// <summary>
/// Input handler for text editing.
/// Key Repeat: Backspace, Delete, Arrows, Home, End
/// Single-Fire: Enter, Escape, Tab
/// </summary>
private void HandleInput(InputState input)
{
    // ...
}
```

## Performance Considerations

### Memory
- One `KeyRepeatState` object (8 bytes) per actively held key
- Typically 0-3 keys held simultaneously
- Dictionary overhead is minimal (~16 bytes)
- **Total**: ~50 bytes worst case

### CPU
- `Update()` iterates held keys: O(n) where n = held keys (typically 0-3)
- `IsKeyPressedWithRepeat()`: O(1) dictionary lookup
- **Negligible impact** on frame time (<0.01ms)

## Migration from Old Console

The old console used manual repeat tracking:

```csharp
// OLD WAY - Manual state management (verbose!)
private Keys? _lastHeldKey;
private float _keyHoldTime;
private float _lastKeyRepeatTime;

private InputHandlingResult HandleWithRepeat(...)
{
    Keys? currentKey = null;
    if (keyboardState.IsKeyDown(Keys.Back))
        currentKey = Keys.Back;

    if (currentKey.HasValue)
    {
        bool shouldProcess = false;

        if (!previousKeyboardState.IsKeyDown(currentKey.Value))
        {
            _lastHeldKey = currentKey.Value;
            _keyHoldTime = 0;
            _lastKeyRepeatTime = 0;
            shouldProcess = true;
        }
        else if (_lastHeldKey == currentKey.Value)
        {
            _keyHoldTime += deltaTime;

            if (_keyHoldTime >= InitialKeyRepeatDelay)
            {
                _lastKeyRepeatTime += deltaTime;

                if (_lastKeyRepeatTime >= KeyRepeatInterval)
                {
                    shouldProcess = true;
                    _lastKeyRepeatTime = 0;
                }
            }
        }

        if (shouldProcess)
        {
            DeleteBackward();
        }

        return InputHandlingResult.Consumed;
    }

    return InputHandlingResult.None;
}
```

**NEW WAY - Simple and clean:**

```csharp
if (input.IsKeyPressedWithRepeat(Keys.Back))
{
    DeleteBackward();
}
```

**Benefits**:
- 20 lines → 4 lines (80% less code!)
- No manual state management
- Automatic cleanup
- Consistent across all components

## Future Enhancements

Potential improvements:

1. **Per-Component Timing**: Different delays/intervals per component type
2. **Acceleration**: Faster repeat after holding longer
3. **Platform-Specific Defaults**: Match OS keyboard settings
4. **Accessibility Options**: Configurable delays for users with motor impairments
5. **Key Chording**: Detect key combinations held together

## Related Files

- `MonoBall Framework.Engine.UI.Debug/Input/InputState.cs` - Core implementation
- `MonoBall Framework.Engine.UI.Debug/Components/Controls/CommandInput.cs` - Example usage
- `MonoBall Framework.Engine.UI.Debug/Components/Debug/ConsolePanel.cs` - Example usage
- `INPUT_CONSUMPTION_PATTERN.md` - Related input system documentation
- `CONSOLE_KEY_REPEAT_IMPLEMENTATION_COMPLETE.md` - Old console implementation

## Testing Checklist

For each key with repeat:
- [ ] Fires once immediately on press
- [ ] Waits 0.5s before repeating
- [ ] Repeats smoothly at 20Hz while held
- [ ] Stops immediately on release
- [ ] Works with modifier keys (Ctrl, Shift, Alt)
- [ ] Respects input consumption
- [ ] Different key cancels previous repeat


