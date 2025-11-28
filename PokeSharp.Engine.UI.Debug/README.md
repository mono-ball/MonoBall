# PokeSharp.Engine.UI.Debug

A comprehensive ImGui-style immediate mode UI framework with constraint-based layout for debug tools. This framework
eliminates coordinate confusion, hardcoded values, and layout calculation duplication that plagued the original debug
console system.

## Overview

This project implements a clean, reusable debug UI system that addresses the core problems of the original
`PokeSharp.Engine.Debug`:

### Problems Solved

1. **❌ Coordinate Confusion** → **✅ Clear Coordinate Spaces**
    - World Space (screen coordinates) vs Container Space (relative to parent)
    - Transformations handled automatically by layout system

2. **❌ Hardcoded Values** → **✅ Theme System**
    - All spacing, colors, and sizes defined in `UITheme`
    - Consistent styling across all components

3. **❌ Layout Duplication** → **✅ Single Source of Truth**
    - Layout calculated ONCE per frame
    - Input handling uses the SAME resolved rectangles
    - No more duplicate calculations between render and input

4. **❌ Non-reusable Code** → **✅ Component System**
    - Build once, use everywhere
    - Composable components with clean interfaces

## Architecture

### Core Systems

#### 1. Layout System (`/Layout`)

**Constraint-Based Positioning**

```csharp
var constraint = new LayoutConstraint
{
    Anchor = Anchor.BottomLeft,  // Where to anchor
    OffsetX = 10,                // Offset from anchor
    Width = 400,                 // Fixed width
    HeightPercent = 0.5f,        // 50% of parent height
    Margin = 10,                 // Space outside
    Padding = 15                 // Space inside
};
```

**Key Classes**:

- `LayoutRect`: Resolved rectangle with absolute coordinates
- `LayoutConstraint`: Positioning rules and size constraints
- `LayoutResolver`: Converts constraints to absolute coordinates
- `LayoutContainer`: Manages child layouts and coordinate transformations

#### 2. Immediate Mode Context (`/Core`)

**Frame Structure**:

```csharp
1. BeginFrame() - Capture input
2. Define UI layout (immediate mode calls)
3. Resolve constraints → absolute rectangles
4. Distribute input (using resolved rects)
5. Render components (using resolved rects)
6. EndFrame()
```

**Key Classes**:

- `UIContext`: Main rendering context, manages frame state
- `UIRenderer`: Low-level drawing (rectangles, text, clipping)
- `UITheme`: Centralized styling (colors, spacing, sizes)
- `UIFrame`: Per-frame state tracking

#### 3. Input System (`/Input`)

**Proper Hit Testing**:

```csharp
// OLD WAY (broken):
// 1. Render using complex calculations
// 2. Click happens
// 3. Try to REVERSE ENGINEER where click should go
// 4. Often wrong due to calculation drift

// NEW WAY (correct):
// 1. Resolve layout ONCE
// 2. Render using resolved rects
// 3. Input uses SAME resolved rects
// 4. Always accurate!
```

**Key Classes**:

- `InputState`: Per-frame input snapshot
- `HitTesting`: Click/hover detection using resolved layouts
- `InputEvent`: Typed input events (click, scroll, key press)

#### 4. Component System (`/Components`)

**Base Classes**:

- `UIComponent`: Base for all components
    - `OnLayout()`: Define constraints
    - `OnRender()`: Draw using resolved rect
    - `OnInput()`: Handle input events
- `UIContainer`: Component that contains children

**Layout Components** (`/Components/Layout`):

- `Panel`: Background rectangle with optional border
- `Stack`: Vertical/horizontal layout
- `ScrollView`: Scrollable container with scrollbar

**Control Components** (`/Components/Controls`):

- `Label`: Single/multi-line text
- `InputField`: Text input with cursor/selection
- `Button`: Clickable button with hover/press states
- `Dropdown`: Auto-complete style suggestions

**Debug Components** (`/Components/Debug`):

- `StatsPanel`: Performance metrics display
- `EntityInspector`: Entity property viewer

## Usage Examples

### Basic Panel

```csharp
public override void Draw(GameTime gameTime)
{
    _uiContext.BeginFrame(inputState);

    // Panel anchored to bottom-left
    var panel = new Panel
    {
        Id = "stats_panel",
        Constraint = new LayoutConstraint
        {
            Anchor = Anchor.BottomLeft,
            Width = 400,
            HeightPercent = 0.5f,
            Margin = 10
        },
        BackgroundColor = theme.BackgroundSecondary,
        BorderColor = theme.BorderPrimary
    };

    // Add children
    panel.AddChild(new Label
    {
        Id = "fps",
        Text = "FPS: 60",
        Color = theme.Success
    });

    panel.Render(_uiContext);

    _uiContext.UpdateInteractionState();
    _uiContext.EndFrame();
}
```

### Interactive Button

```csharp
var button = new Button
{
    Id = "clear_button",
    Text = "Clear Console",
    Constraint = new LayoutConstraint
    {
        Anchor = Anchor.TopLeft,
        Width = 200,
        Height = 30
    },
    OnClick = () =>
    {
        Console.Clear();
        Logger.LogInformation("Console cleared");
    }
};
```

### Scrollable Content

```csharp
var scrollView = new ScrollView
{
    Id = "output",
    Constraint = new LayoutConstraint
    {
        Anchor = Anchor.Fill,
        Margin = 10
    }
};

foreach (var line in outputLines)
{
    scrollView.AddChild(new Label
    {
        Id = $"line_{i}",
        Text = line,
        AutoSize = true
    });
}
```

## Testing

A test scene is provided to demonstrate the system:

```csharp
// Create the test scene
var testScene = new DebugUITestScene(
    GraphicsDevice,
    Services,
    Logger
);

// Push onto scene stack
SceneManager.PushScene(testScene);
```

The test scene shows:

- Panels with different anchor points
- Interactive buttons
- Text input fields
- Stats display
- Responsive layout

Press `F12` to close the test scene.

## Integration with Existing Console

This framework runs **side-by-side** with the existing `PokeSharp.Engine.Debug` console:

1. **Phase 1 (Current)**: Independent testing
    - New framework in separate project
    - Test scene demonstrates capabilities
    - No breaking changes to existing code

2. **Phase 2 (Future)**: Parallel migration
    - Rebuild console using new components
    - Run both systems simultaneously
    - Gradually switch users to new system

3. **Phase 3 (Future)**: Deprecation
    - Remove old console implementation
    - Full migration complete

## Benefits

### Development

- **Faster iteration**: Components are reusable
- **Easier debugging**: Clear coordinate spaces
- **Better testability**: Layout resolution is unit testable

### Performance

- **O(n) constraint resolution** with caching
- **Single layout pass** per frame
- **Efficient rendering** with clipping

### Maintainability

- **No magic numbers**: All values in theme
- **Clear ownership**: Each component manages itself
- **Extensible**: Easy to add new component types

## Comparison with Old System

| Aspect          | Old System                                 | New System                            |
|-----------------|--------------------------------------------|---------------------------------------|
| **Layout**      | Manual pixel calculations everywhere       | Constraint-based, resolved once       |
| **Input**       | Duplicate layout calculations              | Uses same resolved rects              |
| **Coordinates** | Absolute screen coords mixed with relative | Clear separation of coordinate spaces |
| **Styling**     | Scattered constants in multiple files      | Centralized UITheme                   |
| **Reusability** | Copy-paste code for each UI element        | Composable components                 |
| **Hit Testing** | Hardcoded rectangles, often wrong          | Automatic from resolved layout        |

## Architecture Decisions

### Why Immediate Mode?

Immediate mode UI (like Dear ImGui) is perfect for debug tools:

- Simple to use: Define UI in render loop
- No state management: UI rebuilds each frame
- Easy to debug: See entire UI definition in one place
- Perfect for dynamic content: No complex state synchronization

### Why Constraint-Based Layout?

Constraint-based layout provides:

- **Responsive design**: UI adapts to screen size
- **Relative positioning**: Anchors to edges/center automatically
- **Flexible sizing**: Percentage-based or fixed sizes
- **Clean API**: Express intent, not implementation

### Why Separate Coordinate Spaces?

Clear coordinate space separation prevents bugs:

- **World Space**: Top-level screen coordinates
- **Container Space**: Relative to parent's content area
- **Transformations**: Handled by layout system, not manually

## Future Enhancements

Potential additions for future versions:

- [ ] Animation system for smooth transitions
- [ ] More layout containers (Grid, Flex)
- [ ] Rich text rendering (colors, styles)
- [ ] Drag-and-drop support
- [ ] Keyboard navigation
- [ ] Accessibility features
- [ ] Layout debugging visualizer
- [ ] Performance profiler integration

## Dependencies

- MonoGame.Framework.DesktopGL (3.8.4.1)
- FontStashSharp.MonoGame (1.3.9)
- Microsoft.Extensions.Logging.Abstractions (9.0.10)
- PokeSharp.Engine.Common
- PokeSharp.Engine.Scenes

## License

Same as parent project (PokeSharp).




