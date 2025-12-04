# Input Consumption Pattern

## Overview

The UI framework now implements an **input consumption pattern** to prevent child components from processing input that parent components have already handled. This is a standard pattern used in UI frameworks like ImGui, React, and browser DOM events.

## The Problem It Solves

Before this pattern, multiple components could handle the same input event:

```csharp
// Parent component
if (input.IsKeyPressed(Keys.Escape))
{
    CloseAutocomplete(); // Handles Escape
}

// Child component (also sees the same input!)
if (input.IsKeyPressed(Keys.Escape))
{
    OnEscape?.Invoke(); // ALSO handles Escape - double handling!
}
```

This caused issues like:
- Escape closing both autocomplete AND the console
- Enter submitting a command AND accepting a suggestion
- Up/Down navigating both autocomplete AND command history

## How It Works

### 1. InputState Tracks Consumed Input

`InputState` now maintains sets of consumed keys and mouse buttons:

```csharp
private readonly HashSet<Keys> _consumedKeys = new();
private readonly HashSet<MouseButton> _consumedMouseButtons = new();
```

### 2. Parent Components Consume Input After Handling

When a parent component handles input, it marks it as consumed:

```csharp
if (input.IsKeyPressed(Keys.Escape))
{
    HandleEscape(); // Handle the input
    input.ConsumeKey(Keys.Escape); // Mark as consumed
}
```

### 3. Child Components Check Consumed State

`IsKeyPressed()` and `IsMouseButtonPressed()` now check if input was consumed:

```csharp
public bool IsKeyPressed(Keys key)
{
    return !_consumedKeys.Contains(key) &&  // Check if consumed
           _currentKeyboard.IsKeyDown(key) &&
           _previousKeyboard.IsKeyUp(key);
}
```

If a parent consumed the key, children see `IsKeyPressed()` return `false`.

### 4. Consumed State Clears Each Frame

In `InputState.Update()`, consumed input is cleared:

```csharp
public void Update()
{
    // ... update keyboard/mouse state ...

    // Clear consumed input from previous frame
    _consumedKeys.Clear();
    _consumedMouseButtons.Clear();
}
```

## Usage Examples

### Example 1: Parent Handling Autocomplete Navigation

```csharp
// In ConsolePanel - parent component
if (_showSuggestions)
{
    if (input.IsKeyPressed(Keys.Up))
    {
        _suggestionsDropdown.SelectPrevious();
        input.ConsumeKey(Keys.Up); // Prevent command history navigation
    }

    if (input.IsKeyPressed(Keys.Enter))
    {
        _suggestionsDropdown.AcceptSelected();
        input.ConsumeKey(Keys.Enter); // Prevent command submission
    }
}

// In CommandInput - child component
// These won't trigger because keys were consumed:
if (input.IsKeyPressed(Keys.Up))
{
    HistoryPrevious(); // Won't execute if autocomplete consumed it
}

if (input.IsKeyPressed(Keys.Enter))
{
    Submit(); // Won't execute if autocomplete consumed it
}
```

### Example 2: Modal Dialog Consuming All Input

```csharp
// In ModalDialog component
protected override void OnRender(UIContext context)
{
    var input = context.Input;

    // Handle dialog input
    if (input.IsKeyPressed(Keys.Enter))
    {
        AcceptDialog();
        input.ConsumeKey(Keys.Enter);
    }

    if (input.IsKeyPressed(Keys.Escape))
    {
        CancelDialog();
        input.ConsumeKey(Keys.Escape);
    }

    // Consume ALL input to prevent underlying UI from responding
    // (modal dialogs typically block all input to background)
    foreach (var key in Enum.GetValues<Keys>())
    {
        if (input.IsKeyPressed(key))
            input.ConsumeKey(key);
    }

    base.OnRender(context);
}
```

## API Reference

### InputState Methods

#### `void ConsumeKey(Keys key)`
Marks a key as consumed for this frame, preventing child components from seeing it as pressed.

#### `void ConsumeMouseButton(MouseButton button)`
Marks a mouse button as consumed for this frame.

#### `bool IsKeyConsumed(Keys key)`
Checks if a key has been consumed this frame. Useful for debugging or specialized handling.

#### `bool IsMouseButtonConsumed(MouseButton button)`
Checks if a mouse button has been consumed this frame.

### Modified Methods

#### `bool IsKeyPressed(Keys key)`
Now returns `false` if the key was consumed, even if physically pressed.

#### `bool IsMouseButtonPressed(MouseButton button)`
Now returns `false` if the button was consumed, even if physically pressed.

## Best Practices

### 1. Consume After Handling
Always consume input AFTER you've successfully handled it:

```csharp
if (input.IsKeyPressed(Keys.Tab))
{
    // Handle tab completion
    TriggerAutocomplete();

    // Only consume if we actually handled it
    input.ConsumeKey(Keys.Tab);
}
```

### 2. Parent Handles Before Children
The UI framework calls parent `OnRender()` before children, which naturally gives parents priority:

```csharp
protected override void OnRenderContainer(UIContext context)
{
    // 1. Parent handles input first
    HandleParentInput(context.Input);

    // 2. Render children (they see consumed input)
    base.OnRenderContainer(context);
}
```

### 3. Don't Consume Unnecessarily
Only consume input if you actually want to block children from seeing it:

```csharp
// Good: Only consume if autocomplete is visible
if (_showSuggestions && input.IsKeyPressed(Keys.Up))
{
    _suggestionsDropdown.SelectPrevious();
    input.ConsumeKey(Keys.Up);
}

// Bad: Always consuming, even when not handling
if (input.IsKeyPressed(Keys.Up))
{
    input.ConsumeKey(Keys.Up); // Don't do this!
    if (_showSuggestions)
        _suggestionsDropdown.SelectPrevious();
}
```

### 4. Document Consumption Behavior
When creating reusable components, document which inputs they consume:

```csharp
/// <summary>
/// Dropdown menu component.
/// Consumes: Up, Down, Enter, Escape (when open)
/// Consumes: All mouse clicks (when open and clicked)
/// </summary>
public class DropdownMenu : UIComponent
{
    // ...
}
```

## Comparison to Other UI Frameworks

### React (Browser DOM)
```javascript
// React stopPropagation
<div onClick={(e) => {
    handleClick();
    e.stopPropagation(); // Similar to ConsumeMouseButton
}}>
```

### ImGui
```cpp
// ImGui captures input implicitly
if (ImGui::Button("Click Me")) {
    // ImGui automatically "consumes" the click
    // Other widgets won't see it
}
```

### WPF/WinForms
```csharp
// WPF Handled property
protected override void OnKeyDown(KeyEventArgs e)
{
    if (e.Key == Key.Escape)
    {
        CloseDialog();
        e.Handled = true; // Similar to ConsumeKey
    }
}
```

## Debugging

### Check Consumed State
You can check if input was consumed for debugging:

```csharp
if (input.IsKeyConsumed(Keys.Escape))
{
    Console.WriteLine("Escape was consumed by parent component");
}
```

### Log Consumption
Add logging when consuming to trace input flow:

```csharp
if (input.IsKeyPressed(Keys.Escape))
{
    Console.WriteLine($"[{GetType().Name}] Consuming Escape key");
    HandleEscape();
    input.ConsumeKey(Keys.Escape);
}
```

## Future Enhancements

Potential improvements to the pattern:

1. **Input Priority Layers**: Different z-order layers for UI elements
2. **Focus-Based Consumption**: Only focused components can consume input
3. **Conditional Consumption**: Return bool from handlers to indicate if consumed
4. **Input Bubbling Events**: Full event bubbling system like DOM events

## Related Files

- `MonoBall Framework.Engine.UI.Debug/Input/InputState.cs` - Core implementation
- `MonoBall Framework.Engine.UI.Debug/Components/Debug/ConsolePanel.cs` - Example usage
- `AUTOCOMPLETE_BUG_FIXES.md` - Bug that led to this pattern


