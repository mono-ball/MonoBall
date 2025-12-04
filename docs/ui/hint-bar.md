# HintBar Component - Reusable Hint System

## Overview

Created a reusable `HintBar` component for displaying contextual hints and keyboard shortcuts at the bottom of input areas. This follows good UX component patterns and can be reused across the UI framework.

## Problem Solved

### Issue 1: Unwanted Scrolling
When pressing Shift+Enter to create a new line, the editor would scroll to line 2, hiding line 1. This was because `EnsureCursorVisible()` was always trying to optimize scroll position, even when all lines fit on screen.

**Fix**: Added a check to prevent scrolling when all lines fit within the visible area:

```csharp
private void EnsureCursorVisible()
{
    // Calculate visible line count
    var lineHeight = Renderer?.GetLineHeight() ?? 20;
    var visibleHeight = Rect.Height - Padding * 2;
    var visibleLines = Math.Max(1, (int)(visibleHeight / lineHeight));

    // If all lines fit on screen, don't scroll at all
    if (_lines.Count <= visibleLines)
    {
        _scrollOffsetY = 0;
        return;
    }

    // Only scroll if cursor is actually out of view
    // ... existing scroll logic ...
}
```

### Issue 2: Inline Hint Rendering
The TextEditor was rendering hints directly, which wasn't reusable and didn't follow good component patterns. The hint text was also rendering outside the component's bounds.

**Fix**: Created a separate `HintBar` component that can be reused anywhere in the UI.

## HintBar Component Features

### Properties
- **Text**: The hint text to display
- **TextColor**: Color of the hint text (default: dim green)
- **BackgroundColor**: Optional background color (default: transparent)
- **FontSize**: Scale factor for font (default: 1.0)
- **Padding**: Internal padding (default: 4px)

### Methods
- **SetText(string)**: Updates the hint text
- **GetDesiredHeight(UIRenderer?)**: Calculates required height (0 if no text)

### Usage Example

```csharp
// Create a HintBar
var hintBar = new HintBar("my_hints")
{
    TextColor = new Color(100, 200, 100, 200),
    Constraint = new LayoutConstraint
    {
        Anchor = Anchor.BottomLeft,
        OffsetY = 0,
        Width = 600,
        Height = 0 // Dynamically calculated
    }
};

// Update hint text dynamically
hintBar.SetText("Press [F1] for help");

// Calculate and set height
var height = hintBar.GetDesiredHeight(renderer);
hintBar.Constraint.Height = height;
```

## Integration in ConsolePanel

The `ConsolePanel` now uses `HintBar` to show contextual hints:

### Single-line Mode (1 line)
- No hints shown (hint bar height = 0)

### Multi-line Mode (2+ lines)
- Shows: `({count} lines) [Enter] submit • [Shift+Enter] new line`
- Example: `(3 lines) [Enter] submit • [Shift+Enter] new line`

### Dynamic Layout
The console automatically adjusts its layout:
1. **HintBar** shows/hides based on editor state
2. **Input Editor** grows/shrinks (1-10 lines)
3. **Output Buffer** adjusts height to accommodate hints and input
4. **Suggestions Dropdown** positions itself above input + hints

```csharp
// In ConsolePanel.OnRenderContainer()

// Update hint bar based on editor state
if (_commandEditor.IsMultiLine)
{
    _hintBar.SetText($"({_commandEditor.LineCount} lines) [Enter] submit • [Shift+Enter] new line");
}
else
{
    _hintBar.SetText(string.Empty); // Hide hints in single-line mode
}

// Calculate dynamic heights
var inputHeight = _commandEditor.GetDesiredHeight(context.Renderer);
var hintHeight = _hintBar.GetDesiredHeight(context.Renderer);

// Update layouts
_commandEditor.Constraint.Height = inputHeight;
_hintBar.Constraint.Height = hintHeight;
_hintBar.Constraint.OffsetY = -(hintHeight);
_suggestionsDropdown.Constraint.OffsetY = -(inputHeight + hintHeight + Padding);
var outputHeight = contentHeight - inputHeight - hintHeight - Padding;
_outputBuffer.Constraint.Height = outputHeight;
```

## Benefits

### Reusability
- Can be used in any component that needs contextual hints
- Easy to integrate - just add as a child and update text
- Self-contained - handles its own rendering and sizing

### Clean Separation
- TextEditor focuses on editing logic
- HintBar focuses on hint display
- ConsolePanel orchestrates both

### Dynamic Behavior
- Automatically hides when no text is set (height = 0)
- Calculates required height based on font metrics
- Integrates seamlessly with layout system

### Good UX Patterns
- Contextual hints only when relevant
- Non-intrusive visual styling (dim color, transparent background)
- Clear keyboard shortcuts in standardized format

## Future Uses

The HintBar component can be reused for:
- Search boxes: "Type to search • [Ctrl+F] advanced"
- Dialog boxes: "[Enter] confirm • [Esc] cancel"
- Command palette: "↑↓ navigate • [Enter] select"
- File browsers: "[Space] preview • [Del] delete"
- Any UI element that needs contextual help

## Files Modified

- **New**: `/MonoBall Framework.Engine.UI.Debug/Components/Controls/HintBar.cs`
- **Updated**: `/MonoBall Framework.Engine.UI.Debug/Components/Controls/TextEditor.cs`
  - Fixed `EnsureCursorVisible()` to prevent unwanted scrolling
  - Removed inline hint rendering
  - Added `IsMultiLine` property
- **Updated**: `/MonoBall Framework.Engine.UI.Debug/Components/Debug/ConsolePanel.cs`
  - Added `_hintBar` field
  - Integrated HintBar into layout calculation
  - Updates hint text dynamically based on editor state

## Testing

Build Status: ✅ Success (0 warnings, 0 errors)

Test cases:
- [x] Single-line input shows no hints
- [x] Multi-line input shows hint bar
- [x] Hint bar updates line count dynamically
- [x] No unwanted scrolling when creating new lines
- [x] Layout adjusts properly with hints visible
- [x] Suggestions dropdown positions correctly above hints



