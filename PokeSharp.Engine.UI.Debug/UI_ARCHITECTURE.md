# PokeSharp Debug UI Architecture

## Overview

The PokeSharp debug UI framework is a **hybrid immediate-mode/retained-mode UI system** designed specifically for debug
interfaces. It combines the flexibility of immediate mode with the performance benefits of retained mode.

## Architecture Modes

### Immediate Mode

**Immediate mode** means UI components are re-evaluated and re-rendered every frame based on the current application
state.

**Characteristics:**

- No persistent UI state between frames
- Logic and rendering happen in the same pass
- Simple mental model: "What you see is what you render"
- Easy to reason about - no state synchronization issues

**Example:**

```csharp
void Update()
{
    // Immediate mode: Create and render UI every frame
    if (ImGui.Button("Click Me"))
    {
        DoSomething();
    }
}
```

### Retained Mode

**Retained mode** means UI components maintain their own state and structure. The application creates the UI hierarchy
once, and the framework manages updates.

**Characteristics:**

- UI objects persist across frames
- Separation of UI structure from rendering
- Event-driven updates
- More complex state management
- Better performance for large, stable UIs

**Example:**

```csharp
void Initialize()
{
    // Retained mode: Create UI hierarchy once
    var button = new Button("Click Me");
    button.OnClick = DoSomething;
    panel.AddChild(button);
}

void Update()
{
    // Just render existing UI
    panel.Render();
}
```

## PokeSharp's Hybrid Approach

Our debug UI framework uses a **hybrid approach** that combines both paradigms:

### Retained Structure + Immediate Rendering

1. **Retained Component Tree**
    - UI components (`ConsolePanel`, `TextEditor`, `TextBuffer`, etc.) are created once and persist
    - Component hierarchy is established at initialization
    - Components maintain their own state (text, cursor position, scroll offset, etc.)

2. **Immediate Mode Rendering**
    - Every frame, components re-evaluate their visual state
    - Layout calculations happen per-frame based on constraints
    - Rendering commands are issued immediately during traversal
    - No display list caching

### Benefits of This Approach

**From Retained Mode:**

- ✅ Efficient state management (text buffers, history, etc.)
- ✅ Event system (OnSubmit, OnTextChanged, etc.)
- ✅ Component reusability
- ✅ Hierarchical structure

**From Immediate Mode:**

- ✅ Flexible layout (dynamic resizing, animation)
- ✅ Responsive to state changes (auto-scroll, dynamic hints)
- ✅ Simple rendering path
- ✅ Easy debugging (what you see is what's currently rendering)

## Component Architecture

### Base Classes

```
UIComponent (abstract)
├── UIContainer (can have children)
│   ├── Panel
│   ├── StackPanel
│   └── ConsolePanel
└── Control (leaf components)
    ├── TextBuffer
    ├── TextEditor
    ├── Button
    └── SuggestionsDropdown
```

### Key Components

#### UIComponent

Base class for all UI elements. Handles:

- Layout constraint evaluation
- Focus management
- Mouse hover detection
- Event propagation

#### UIContainer

Extends `UIComponent` to support child components:

- Child management (add/remove)
- Hierarchical rendering
- Layout calculation for children
- Input routing

#### UIContext

Provides rendering environment and shared state:

- Renderer access
- Input state
- Focus tracking
- Frame state

### Rendering Pipeline

1. **Layout Phase** (per frame)
   ```
   Component.ResolveLayout(parentRect, context)
   ├── Apply constraints (anchor, offset, size)
   ├── Calculate absolute rectangle
   └── Store in component.Rect
   ```

2. **Render Phase** (per frame)
   ```
   Component.Render(context)
   ├── OnRenderSelf() - draw this component
   ├── OnRenderChildren() - draw child components
   └── OnRenderOverlay() - draw on top (tooltips, etc.)
   ```

3. **Input Phase** (per frame)
   ```
   Component.ProcessInput(inputEvent)
   ├── Check hover state
   ├── Handle mouse events
   ├── Handle keyboard events (if focused)
   └── Propagate to children
   ```

## State Management

### Component State (Retained)

Components maintain their own state:

- `TextEditor`: text content, cursor position, selection
- `TextBuffer`: line storage, scroll position, search results
- `ConsolePanel`: visibility, size, search mode

### Frame State (Immediate)

Some state is recalculated every frame:

- Layout rectangles (based on constraints)
- Hover states (based on mouse position)
- Animation values (interpolated per frame)
- Dynamic heights (multi-line input, suggestions)

### Theme System (Static)

Visual styling is centralized in `UITheme`:

- Colors (background, text, borders, etc.)
- Sizes (padding, margins, font size, etc.)
- Animation speeds (blink rate, slide speed, etc.)

## Layout System

### Constraints

Components use `LayoutConstraint` to define their positioning:

```csharp
new LayoutConstraint
{
    Anchor = Anchor.StretchTop,  // How to position/size
    OffsetX = 0,                  // X offset from anchor
    OffsetY = 0,                  // Y offset from anchor
    Width = 600,                  // Fixed width (or 0 for stretch)
    Height = 400                  // Fixed height (or 0 for stretch)
}
```

### Anchor System

Anchors define how a component is positioned relative to its parent:

- **Stretch Anchors**: `StretchTop`, `StretchBottom`, `StretchLeft`, `StretchRight`
    - Component stretches to fill available space in one or both dimensions

- **Corner Anchors**: `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight`
    - Component is positioned at a specific corner

- **Center Anchors**: `MiddleCenter`, `MiddleLeft`, `MiddleRight`
    - Component is centered along one or both axes

### Dynamic Layout

Some layouts are recalculated every frame:

**Example: ConsolePanel**

- Input editor height changes based on line count (multi-line)
- Hint bar appears/disappears dynamically
- Suggestions dropdown resizes based on item count
- Search bar replaces input when active

This is where the **immediate mode** aspect shines - layout responds instantly to state changes.

## Event System

### Event Flow

1. Input events captured by `InputState`
2. Routed to focused/hovered component via `UIContext`
3. Component handles event or propagates to parent
4. Custom events fired via delegates (`OnSubmit`, `OnTextChanged`, etc.)

### Custom Events

Components expose action delegates for important state changes:

```csharp
public Action<string>? OnCommandSubmitted { get; set; }
public Action<string>? OnTextChanged { get; set; }
public Action? OnCloseRequested { get; set; }
```

This maintains the **retained mode** event-driven paradigm while keeping components decoupled.

## Performance Considerations

### Why This Works Well for Debug UI

1. **Limited Scope**
    - Debug UIs typically have < 100 components
    - Not rendering game world simultaneously
    - Performance is not critical (debug tools)

2. **State Locality**
    - Most state is component-local
    - No global state synchronization
    - Easy to reason about

3. **Layout Flexibility**
    - Dynamic layouts (animations, auto-sizing) are easy
    - No need to invalidate/reflow on changes
    - Constraints are re-evaluated cheaply each frame

4. **Simple Rendering**
    - Direct rendering calls (no display list)
    - No batching complexity
    - Easy to debug visual issues

### Performance Optimizations

1. **Culling**: Off-screen components skip rendering
2. **Lazy Calculation**: Layout only when visible
3. **Input Routing**: Events only to relevant components
4. **Text Caching**: Font metrics cached per string (in FontStashSharp)

## Comparison to Other Approaches

### vs. Pure Immediate Mode (ImGui)

**Advantages:**

- ✅ Better state management (text buffers, history)
- ✅ Component reusability
- ✅ Event system

**Disadvantages:**

- ❌ More boilerplate (create components)
- ❌ More complex structure

### vs. Pure Retained Mode (WPF, Qt)

**Advantages:**

- ✅ Simpler layout system
- ✅ No property change notifications
- ✅ Easier to debug

**Disadvantages:**

- ❌ Layout every frame (vs. on invalidation)
- ❌ No display list caching

## Best Practices

### When to Use Retained State

- Persistent data (text content, history, bookmarks)
- Component structure (hierarchy of panels/controls)
- User preferences (theme, size, position)

### When to Use Immediate Evaluation

- Layout calculations (dynamic sizing, positioning)
- Visual state (hover, blink, animation)
- Input handling (cursor position from mouse)

### Component Design

1. **Keep state minimal** - only store what persists across frames
2. **Use properties** - expose state through properties, not fields
3. **Events for important changes** - fire events when state changes significantly
4. **Constraints for layout** - use LayoutConstraint instead of manual positioning
5. **Theme for styling** - never hardcode colors or sizes

## Future Enhancements

### Potential Improvements

1. **Layout Caching** - cache layout when constraints unchanged
2. **Dirty Rectangles** - only re-render changed regions
3. **Display Lists** - cache rendering commands for static content
4. **Batch Rendering** - group similar draw calls

However, these optimizations are likely **not needed** for debug UI given current performance is excellent.

## Conclusion

The hybrid immediate/retained architecture provides an excellent balance for debug UI:

- **Fast development** - components are easy to create and compose
- **Good performance** - adequate for debug UI scope
- **Easy debugging** - clear rendering path and state management
- **Flexible layout** - dynamic sizing and animation work naturally

This architecture is **not suitable** for production game UI (use retained mode with caching), but is **ideal** for
debug tools where flexibility and development speed are priorities.

