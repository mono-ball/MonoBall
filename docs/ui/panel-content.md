# Panel Content Reference Guide

> **Comprehensive guide to building consistent panel content using layout constants, utility components, and icons.**

## Table of Contents
- [Layout Constants](#layout-constants)
- [NerdFont Icons Reference](#nerdfont-icons-reference)
- [Utility Components](#utility-components)
- [Theme System](#theme-system)
- [Common Patterns](#common-patterns)

---

## Layout Constants

### PanelConstants.cs

Centralized layout constants for consistent panel sizing and spacing.

#### Profiler Panel Constants
```csharp
PanelConstants.Profiler.BottomPadding       // 24f  - Extra space at bottom
PanelConstants.Profiler.NameColumnWidth     // 200f - Width for system names
PanelConstants.Profiler.MsColumnWidth       // 100f - Width for millisecond values
```

**Usage Example:**
```csharp
float nameColumnX = contentRect.X;
float msColumnX = contentRect.X + PanelConstants.Profiler.NameColumnWidth;
float barX = msColumnX + PanelConstants.Profiler.MsColumnWidth;
```

#### Stats Panel Constants
```csharp
PanelConstants.Stats.LabelWidth    // 100f - Width for stat labels
PanelConstants.Stats.BarWidth      // 150f - Width for progress bars
PanelConstants.Stats.ValueOffset   // 80f  - Offset from label to value text
PanelConstants.Stats.RowSpacing    // 4f   - Additional vertical spacing per row
```

**Usage Example:**
```csharp
float labelX = contentRect.X;
float valueX = labelX + PanelConstants.Stats.ValueOffset;
float barX = valueX + 50f;
float barWidth = PanelConstants.Stats.BarWidth;
```

---

## NerdFont Icons Reference

### Tree/Hierarchy Indicators

| Icon | Constant | Unicode | Usage |
|------|----------|---------|-------|
| ⌄ | `NerdFontIcons.Expanded` | `\uEAB4` | Expanded tree node |
| ❯ | `NerdFontIcons.Collapsed` | `\uEAB6` | Collapsed tree node |
| 📂 | `NerdFontIcons.FolderOpen` | `\uF07C` | Open folder |
| 📁 | `NerdFontIcons.FolderClosed` | `\uF07B` | Closed folder |
| ─ | `NerdFontIcons.TreeDash` | `\uEAD4` | Tree branch |

**Pre-built Combos:**
```csharp
NerdFontIcons.ExpandedWithSpace   // "⌄ "
NerdFontIcons.CollapsedWithSpace  // "❯ "
```

### Selection & Cursor

| Icon | Constant | Unicode | Usage |
|------|----------|---------|-------|
| ▶ | `NerdFontIcons.SelectionPointer` | `\uEA9C` | Selected item |
| ▌ | `NerdFontIcons.Cursor` | `"▌"` | Text cursor |
|  | `NerdFontIcons.PromptChevron` | `\uE0B0` | Prompt separator |
| → | `NerdFontIcons.PromptArrow` | `\uEA9C` | Prompt arrow |
| λ | `NerdFontIcons.PromptLambda` | `"λ"` | Lambda prompt |
| ❯ | `NerdFontIcons.PromptSymbol` | `\uF460` | Prompt symbol |

**Pre-built Combos:**
```csharp
NerdFontIcons.Prompt              // "❯ "
NerdFontIcons.SelectedWithSpace   // "▶ "
NerdFontIcons.UnselectedSpace     // "  "
```

### Status Indicators

| Icon | Constant | Unicode | Usage |
|------|----------|---------|-------|
| ✓ | `NerdFontIcons.Success` | `\uF00C` | Success checkmark |
| ✓ | `NerdFontIcons.SuccessCircle` | `\uF058` | Success with circle |
| ✗ | `NerdFontIcons.Error` | `\uF00D` | Error X |
| ✗ | `NerdFontIcons.ErrorCircle` | `\uF057` | Error with circle |
| ⚠ | `NerdFontIcons.Warning` | `\uF071` | Warning triangle |
| ⚠ | `NerdFontIcons.AlertCircle` | `\uF06A` | Alert circle |
| ℹ | `NerdFontIcons.Info` | `\uF05A` | Info circle |
| ? | `NerdFontIcons.Question` | `\uF059` | Question circle |
| 📌 | `NerdFontIcons.Pinned` | `\uF435` | Pinned item |
| ⭐ | `NerdFontIcons.Star` | `\uF005` | Favorite/star |
| 🔥 | `NerdFontIcons.Flame` | `\uF06D` | Hot/fire |
| ⚡ | `NerdFontIcons.Bolt` | `\uF0E7` | Lightning |

**Pre-built Combos:**
```csharp
NerdFontIcons.PinnedHeader        // "📌 PINNED"
NerdFontIcons.StatusHealthy       // "✓"
NerdFontIcons.StatusWarning       // "⚠"
NerdFontIcons.StatusError         // "✗"
```

### Arrows & Navigation

| Icon | Constant | Unicode | Usage |
|------|----------|---------|-------|
| ↑ | `NerdFontIcons.ArrowUp` | `\uF062` | Move/sort up |
| ↓ | `NerdFontIcons.ArrowDown` | `\uF063` | Move/sort down |
| ← | `NerdFontIcons.ArrowLeft` | `\uF060` | Previous |
| → | `NerdFontIcons.ArrowRight` | `\uF061` | Next |
| ▲ | `NerdFontIcons.CaretUp` | `\uF0D8` | Collapse/up |
| ▼ | `NerdFontIcons.CaretDown` | `\uF0D7` | Expand/down |
| ◀ | `NerdFontIcons.CaretLeft` | `\uF0D9` | Previous item |
| ▶ | `NerdFontIcons.CaretRight` | `\uF0DA` | Next item |
| ⇞ | `NerdFontIcons.ScrollUp` | `\uF102` | Scroll up |
| ⇟ | `NerdFontIcons.ScrollDown` | `\uF103` | Scroll down |
| ▼ | `NerdFontIcons.DropdownArrow` | `\uF0D7` | Dropdown menu |

### Actions & Commands

| Icon | Constant | Unicode | Usage |
|------|----------|---------|-------|
| ▶ | `NerdFontIcons.Play` | `\uF04B` | Play/run |
| ⏸ | `NerdFontIcons.Pause` | `\uF04C` | Pause |
| ⏹ | `NerdFontIcons.Stop` | `\uF04D` | Stop |
| ⏭ | `NerdFontIcons.StepForward` | `\uF051` | Step next |
| ↻ | `NerdFontIcons.Refresh` | `\uF021` | Refresh/reload |
| ⟳ | `NerdFontIcons.Sync` | `\uF021` | Sync |
| ⟳ | `NerdFontIcons.Spinner` | `\uF110` | Loading spinner |
| 🔍 | `NerdFontIcons.Search` | `\uF002` | Search |
| ⚙ | `NerdFontIcons.Settings` | `\uF013` | Settings/gear |
| ⚙⚙ | `NerdFontIcons.Gears` | `\uF085` | Multiple gears |
| ✗ | `NerdFontIcons.Close` | `\uF00D` | Close/dismiss |
| + | `NerdFontIcons.Add` | `\uF067` | Add/create |
| − | `NerdFontIcons.Remove` | `\uF068` | Remove/delete |
| ✎ | `NerdFontIcons.Edit` | `\uF040` | Edit/pencil |
| 📋 | `NerdFontIcons.Copy` | `\uF24D` | Copy/clone |
| 📋 | `NerdFontIcons.Paste` | `\uF0EA` | Paste |
| 🗑 | `NerdFontIcons.Trash` | `\uF1F8` | Delete/trash |
| 💾 | `NerdFontIcons.Save` | `\uF0C7` | Save |
| ↶ | `NerdFontIcons.Undo` | `\uF0E2` | Undo |
| ↷ | `NerdFontIcons.Redo` | `\uF01E` | Redo |

### Data & Objects

| Icon | Constant | Unicode | Usage |
|------|----------|---------|-------|
| 🔤 | `NerdFontIcons.Variable` | `\uEA88` | Variable/symbol |
| [] | `NerdFontIcons.Array` | `\uEA8A` | Array/list |
| {} | `NerdFontIcons.Object` | `\uEB5B` | Object/class |
| ƒ | `NerdFontIcons.Function` | `\uEA8C` | Function/method |
| ⚑ | `NerdFontIcons.Property` | `\uEB65` | Property |
| ⚏ | `NerdFontIcons.Field` | `\uEB5F` | Field |
| 🔑 | `NerdFontIcons.Keyword` | `\uEB62` | Keyword |
| 📊 | `NerdFontIcons.Constant` | `\uEB5D` | Constant |
| " | `NerdFontIcons.StringType` | `\uEB8D` | String type |
| # | `NerdFontIcons.NumberType` | `\uEB64` | Number type |
| ✓✗ | `NerdFontIcons.BooleanType` | `\uEB5C` | Boolean type |
| ∅ | `NerdFontIcons.Null` | `\uEB63` | Null/empty |
| ⚐ | `NerdFontIcons.Enum` | `\uEA95` | Enum |
| ⚏ | `NerdFontIcons.Interface` | `\uEB61` | Interface |
| 📦 | `NerdFontIcons.Namespace` | `\uEA8B` | Namespace |

### Entities & Game

| Icon | Constant | Unicode | Usage |
|------|----------|---------|-------|
| ◼ | `NerdFontIcons.Entity` | `\uF1B2` | Entity/cube |
| ◼◼ | `NerdFontIcons.Entities` | `\uF1B3` | Multiple entities |
| 🧩 | `NerdFontIcons.Component` | `\uF12E` | Component/puzzle |
| 👤 | `NerdFontIcons.Player` | `\uF007` | Player/user |
| 👥 | `NerdFontIcons.NPC` | `\uF2C0` | NPC |
| 👥👥 | `NerdFontIcons.Users` | `\uF0C0` | Group/users |
| 🌐 | `NerdFontIcons.World` | `\uF0AC` | World/globe |
| 📍 | `NerdFontIcons.Map` | `\uF041` | Map marker |
| 🗺 | `NerdFontIcons.MapOutline` | `\uF278` | Map outline |
| 🎮 | `NerdFontIcons.Gamepad` | `\uF11B` | Gamepad |

### Debugging & Profiling

| Icon | Constant | Unicode | Usage |
|------|----------|---------|-------|
| 🐛 | `NerdFontIcons.Bug` | `\uF188` | Bug/debug |
| 🔍 | `NerdFontIcons.Debug` | `\uEA87` | Debug mode |
| ⏺ | `NerdFontIcons.Breakpoint` | `\uEAB1` | Breakpoint |
| 👁 | `NerdFontIcons.Watch` | `\uF06E` | Watch variable |
| 👁‍🗨 | `NerdFontIcons.Hidden` | `\uF070` | Hidden/eye slash |
| ⏱ | `NerdFontIcons.Timer` | `\uF017` | Timer/clock |
| ⏱ | `NerdFontIcons.Stopwatch` | `\uF439` | Stopwatch |
| 📊 | `NerdFontIcons.Performance` | `\uF0E4` | Performance dash |
| 🖥 | `NerdFontIcons.Memory` | `\uF2DB` | Memory/chip |
| 🖥 | `NerdFontIcons.Server` | `\uF233` | Server |
| 💾 | `NerdFontIcons.Database` | `\uF1C0` | Database |

### Logs & Output

| Icon | Constant | Unicode | Usage |
|------|----------|---------|-------|
| 📄 | `NerdFontIcons.Log` | `\uF0F6` | Log/file |
| 📄⚠ | `NerdFontIcons.FileAlert` | `\uF0F6` | File warning |
| 💻 | `NerdFontIcons.Console` | `\uF120` | Console/terminal |
| 📤 | `NerdFontIcons.Output` | `\uEB9D` | Output/export |
| 🕐 | `NerdFontIcons.History` | `\uF1DA` | History/clock |
| ≡ | `NerdFontIcons.List` | `\uF03A` | List |
| ☰ | `NerdFontIcons.ListAlt` | `\uF022` | Alt list |
| 🔽 | `NerdFontIcons.Filter` | `\uF0B0` | Filter |

### File Types

| Icon | Constant | Unicode | Usage |
|------|----------|---------|-------|
| # | `NerdFontIcons.CSharp` | `\uE648` | C# file |
| {} | `NerdFontIcons.Json` | `\uE60B` | JSON file |
| ⚙ | `NerdFontIcons.Config` | `\uE615` | Config file |
| 📜 | `NerdFontIcons.Script` | `\uE614` | Script file |
| 🖼 | `NerdFontIcons.Image` | `\uF1C5` | Image file |
| 💻 | `NerdFontIcons.Code` | `\uF1C9` | Code file |

### Misc Symbols

| Icon | Constant | Unicode | Usage |
|------|----------|---------|-------|
| … | `NerdFontIcons.Ellipsis` | `\u2026` | Horizontal ellipsis |
| ● | `NerdFontIcons.Bullet` | `\uF111` | Bullet point |
| ● | `NerdFontIcons.Dot` | `\uEAAB` | Filled circle |
| ○ | `NerdFontIcons.DotOutline` | `\uF10C` | Circle outline |
| ■ | `NerdFontIcons.Square` | `\uF0C8` | Square |
| □ | `NerdFontIcons.SquareOutline` | `\uF096` | Square outline |
| ☑ | `NerdFontIcons.CheckSquare` | `\uF14A` | Checked square |
| ☐ | `NerdFontIcons.CheckSquareOutline` | `\uF046` | Unchecked square |

**Box Drawing:**
```csharp
NerdFontIcons.Separator       // "│" - vertical line
NerdFontIcons.TreeBranch      // "├" - branch
NerdFontIcons.TreeLast        // "└" - last item
NerdFontIcons.TreeHorizontal  // "─" - horizontal
NerdFontIcons.TreeVertical    // "│" - vertical
```

**Powerline:**
```csharp
NerdFontIcons.PowerlineLeft   // "\uE0B0"
NerdFontIcons.PowerlineRight  // "\uE0B2"
```

---

## Utility Components

### 1. ScrollbarComponent

**Purpose:** Consistent scrolling behavior across all UI components with drag support.

#### Basic Usage
```csharp
private readonly ScrollbarComponent _scrollbar = new();

// In HandleInput:
bool scrollChanged = _scrollbar.HandleMouseWheel(input, contentHeight, visibleHeight);
scrollChanged |= _scrollbar.HandleInput(
    context, input, scrollbarRect, contentHeight, visibleHeight, "my-panel-id"
);

// In Draw:
_scrollbar.Draw(renderer, theme, scrollbarRect, contentHeight, visibleHeight);
```

#### Properties
```csharp
float ScrollOffset { get; set; }   // Current scroll position (0 = top)
bool IsDragging { get; }           // Whether currently dragging
```

#### Methods

**HandleInput** - Drag and click scrolling
```csharp
bool HandleInput(
    UIContext context,
    InputState input,
    LayoutRect trackRect,        // Scrollbar track area
    float contentHeight,          // Total scrollable content
    float visibleHeight,          // Visible viewport
    string componentId            // For input capture
)
```

**HandleMouseWheel** - Pixel-based wheel scrolling
```csharp
bool HandleMouseWheel(
    InputState input,
    float contentHeight,
    float visibleHeight,
    int? pixelsPerTick = null     // Override theme.ScrollSpeed
)
```

**HandleMouseWheelLines** - Line-based wheel scrolling
```csharp
bool HandleMouseWheelLines(
    InputState input,
    int totalLines,
    int visibleLines,
    int? linesPerTick = null      // Override theme.ScrollWheelSensitivity
)
```

**Draw** - Render scrollbar
```csharp
void Draw(
    UIRenderer renderer,
    UITheme theme,
    LayoutRect trackRect,
    float contentHeight,
    float visibleHeight
)
```

**Utility Methods:**
```csharp
void ClampOffset(float contentHeight, float visibleHeight)
void Reset()                                              // Reset to top
void ScrollToTop()
void ScrollToBottom(float contentHeight, float visibleHeight)
static bool IsNeeded(float contentHeight, float visibleHeight)
```

#### Complete Example
```csharp
private readonly ScrollbarComponent _scrollbar = new();

public void HandleInput(UIContext context, InputState input)
{
    // Mouse wheel scrolling
    if (_scrollbar.HandleMouseWheel(input, _totalHeight, _visibleHeight))
    {
        // Content scrolled
    }

    // Scrollbar drag/click
    var scrollbarRect = new LayoutRect(
        contentRect.Right - theme.ScrollbarWidth,
        contentRect.Y,
        theme.ScrollbarWidth,
        contentRect.Height
    );

    _scrollbar.HandleInput(context, input, scrollbarRect,
        _totalHeight, _visibleHeight, "my-panel");
}

public void Draw(UIRenderer renderer, UITheme theme)
{
    // Draw scrollbar if needed
    if (ScrollbarComponent.IsNeeded(_totalHeight, _visibleHeight))
    {
        var scrollbarRect = new LayoutRect(
            contentRect.Right - theme.ScrollbarWidth,
            contentRect.Y,
            theme.ScrollbarWidth,
            contentRect.Height
        );
        _scrollbar.Draw(renderer, theme, scrollbarRect,
            _totalHeight, _visibleHeight);
    }
}
```

---

### 2. EmptyStateComponent

**Purpose:** Standardized empty state displays for consistent UX.

#### Methods

**DrawCentered** - Centered empty state with icon
```csharp
EmptyStateComponent.DrawCentered(
    renderer,
    theme,
    area,
    "No items found",                    // Title
    "Try adjusting your filters",        // Description (optional)
    NerdFontIcons.FileAlert              // Icon (optional)
);
```

**DrawLeftAligned** - Left-aligned empty state
```csharp
float nextY = EmptyStateComponent.DrawLeftAligned(
    renderer,
    theme,
    x,
    y,
    "No logs available",
    "Logs will appear here when generated"
);
```

**DrawLoading** - Loading state with spinner
```csharp
EmptyStateComponent.DrawLoading(
    renderer,
    theme,
    area,
    "Loading data...",
    "Please wait"
);
```

**DrawNoData** - No data state with file icon
```csharp
EmptyStateComponent.DrawNoData(
    renderer,
    theme,
    area,
    "No events recorded",
    "Events will appear once they are triggered"
);
```

**DrawError** - Error state with alert icon
```csharp
EmptyStateComponent.DrawError(
    renderer,
    theme,
    area,
    "Failed to load data",
    "Check your connection and try again"
);
```

#### Common Patterns
```csharp
// In Draw method:
if (_items.Count == 0)
{
    if (_isLoading)
    {
        EmptyStateComponent.DrawLoading(renderer, theme, contentRect,
            "Loading entities...");
    }
    else if (_hasError)
    {
        EmptyStateComponent.DrawError(renderer, theme, contentRect,
            "Failed to load entities",
            "Check console for details");
    }
    else
    {
        EmptyStateComponent.DrawNoData(renderer, theme, contentRect,
            "No entities found",
            "Entities will appear when they are created");
    }
    return;
}

// Draw actual content...
```

---

### 3. SortableTableHeader<TSort>

**Purpose:** Reusable sortable table headers with click handling.

#### Basic Usage
```csharp
// Define sort enum
public enum EntitySort
{
    IdAsc, IdDesc,
    NameAsc, NameDesc,
    TypeAsc, TypeDesc
}

// Create header
private readonly SortableTableHeader<EntitySort> _header;

public MyPanel()
{
    _header = new SortableTableHeader<EntitySort>(EntitySort.IdAsc);
    _header.SortChanged += sort => {
        _currentSort = sort;
        ResortItems();
    };

    _header.AddColumns(
        new Column { Label = "ID", SortMode = EntitySort.IdAsc, X = 10f, Ascending = true },
        new Column { Label = "ID", SortMode = EntitySort.IdDesc, X = 10f, Ascending = false },
        new Column { Label = "Name", SortMode = EntitySort.NameAsc, X = 100f, MaxWidth = 200f },
        new Column { Label = "Type", SortMode = EntitySort.TypeAsc, X = 320f }
    );
}
```

#### Column Properties
```csharp
public record Column
{
    string Label { get; init; }              // Column text
    TSort SortMode { get; init; }            // Associated sort mode
    float X { get; init; }                   // X position
    float? MaxWidth { get; init; }           // Width for alignment
    bool Ascending { get; init; }            // Show ↑ or ↓
    string? CustomIcon { get; init; }        // Override default icon
    HorizontalAlignment Alignment { get; init; }  // Left/Center/Right
}
```

#### Methods
```csharp
void AddColumn(Column column)
void AddColumns(params Column[] columns)
void ClearColumns()
void SetSort(TSort sortMode)                 // Set without triggering event
bool HandleInput(InputState input)           // Returns true if sort changed
void Draw(UIRenderer renderer, UITheme theme, float y, float lineHeight)
void DrawWithHover(UIRenderer renderer, UITheme theme, InputState input,
                   float y, float lineHeight)
```

#### Complete Example
```csharp
private readonly SortableTableHeader<EntitySort> _header;

public MyPanel()
{
    _header = new(EntitySort.IdAsc);
    _header.SortChanged += OnSortChanged;

    // Configure columns
    _header.AddColumns(
        new() {
            Label = "ID",
            SortMode = EntitySort.IdAsc,
            X = contentX,
            MaxWidth = 80f,
            Ascending = true,
            Alignment = HorizontalAlignment.Left
        },
        new() {
            Label = "Name",
            SortMode = EntitySort.NameAsc,
            X = contentX + 100f,
            MaxWidth = 200f
        }
    );
}

private void OnSortChanged(EntitySort newSort)
{
    _currentSort = newSort;
    SortEntities();
}

protected override bool OnHandleInput(UIContext context, InputState input)
{
    return _header.HandleInput(input);
}

protected override void OnRender(UIContext context)
{
    float headerY = contentRect.Y;
    int lineHeight = Renderer.GetLineHeight();

    // Draw header with hover effects
    _header.DrawWithHover(Renderer, Theme, input, headerY, lineHeight);

    // Draw items below header...
    float itemY = headerY + lineHeight + Theme.SpacingNormal;
}
```

---

### 4. Sparkline

**Purpose:** Mini bar charts for time-series data with color-coded thresholds.

#### Factory Methods
```csharp
// Frame time sparkline (16.67ms reference line)
var frameTimeSparkline = Sparkline.ForFrameTime("frame-time", bufferSize: 60);

// FPS sparkline (60fps reference)
var fpsSparkline = Sparkline.ForFps("fps", bufferSize: 60);

// Percentage sparkline (0-100%)
var percentSparkline = Sparkline.ForPercentage("memory-usage",
    bufferSize: 60, warningPercent: 80f, errorPercent: 95f);
```

#### Configuration Methods
```csharp
// Chaining API for configuration
sparkline
    .WithScale(minValue, maxValue)           // Fixed scale (null = auto)
    .WithReferenceLine(60f, Color.Yellow)    // Horizontal reference line
    .WithThresholds(warning: 50f, error: 80f)
    .WithColors(good: Color.Green, warning: Color.Yellow, error: Color.Red)
    .WithBackground(Color.Black)
    .WithBorder(Color.Gray, show: true)
    .WithBarGap(2f);
```

#### Data Management
```csharp
sparkline.AddValue(16.5f);                   // Add data point
sparkline.Clear();                           // Clear all data

int count = sparkline.DataCount;             // Current data count
int size = sparkline.BufferSize;             // Total buffer size
float? latest = sparkline.LatestValue;       // Most recent value

var (min, max, avg) = sparkline.GetStatistics();
```

#### Rendering
```csharp
// Inline rendering (no layout)
sparkline.Draw(renderer, x: 100f, y: 50f, width: 200f, height: 40f);

// Or use as UIComponent with layout
sparkline.Rect = new LayoutRect(100f, 50f, 200f, 40f);
sparkline.Render(context);
```

#### Complete Example
```csharp
private readonly Sparkline _frameTimeSparkline;
private readonly Sparkline _memorySparkline;

public MyPanel()
{
    // Frame time sparkline with 16.67ms budget line
    _frameTimeSparkline = Sparkline.ForFrameTime("frame-time")
        .WithThresholds(warning: 16.67f, error: 25f);

    // Memory usage sparkline
    _memorySparkline = Sparkline.ForPercentage("memory")
        .WithThresholds(warning: 75f, error: 90f);
}

public void Update(GameTime gameTime)
{
    // Add new data points
    _frameTimeSparkline.AddValue((float)gameTime.ElapsedGameTime.TotalMilliseconds);
    _memorySparkline.AddValue(GetMemoryUsagePercent());
}

protected override void OnRender(UIContext context)
{
    float y = contentRect.Y;

    // Draw frame time label + sparkline
    Renderer.DrawText("Frame Time:", contentRect.X, y, Theme.TextPrimary);
    _frameTimeSparkline.Draw(Renderer,
        contentRect.X + 120f, y, width: 200f, height: 30f);

    y += 40f;

    // Draw memory label + sparkline
    Renderer.DrawText("Memory:", contentRect.X, y, Theme.TextPrimary);
    _memorySparkline.Draw(Renderer,
        contentRect.X + 120f, y, width: 200f, height: 30f);
}
```

---

### 5. TextUtils

**Purpose:** Text truncation with ellipsis for constrained layouts.

#### Methods

**TruncateWithEllipsis** - Smart truncation with "…" suffix
```csharp
string truncated = renderer.TruncateWithEllipsis(
    "This is a very long entity name that won't fit",
    maxWidth: 150f
);
// Result: "This is a very lon…"
```

**TruncateWithSuffix** - Custom suffix
```csharp
string truncated = renderer.TruncateWithSuffix(
    "VeryLongSystemName",
    maxWidth: 100f,
    suffix: "..."
);
// Result: "VeryLongSys..."
```

#### Usage Example
```csharp
// Truncate entity names to fit column width
foreach (var entity in entities)
{
    string displayName = renderer.TruncateWithEllipsis(
        entity.Name,
        maxWidth: PanelConstants.Profiler.NameColumnWidth
    );

    renderer.DrawText(displayName, x, y, theme.TextPrimary);
    y += lineHeight;
}
```

---

## Theme System

### Accessing Theme
```csharp
UITheme theme = ThemeManager.Current;  // Get active theme
```

### Available Themes
```csharp
UITheme.OneDark          // Default dark theme (VS Code)
UITheme.Monokai          // Sublime Text classic
UITheme.Dracula          // Popular dark theme
UITheme.GruvboxDark      // Retro warm colors
UITheme.Nord             // Arctic cool colors
UITheme.SolarizedDark    // Ethan Schoonover's classic
UITheme.SolarizedLight   // Light variant
UITheme.Pokeball         // Pokémon-inspired (red/yellow)
```

### Color Categories

#### Spacing Constants
```csharp
// Padding
theme.PaddingTiny        // 2f
theme.PaddingSmall       // 4f
theme.PaddingMedium      // 8f
theme.PaddingLarge       // 12f
theme.PaddingXLarge      // 20f

// Margins
theme.MarginTiny         // 2f
theme.MarginSmall        // 4f
theme.MarginMedium       // 8f
theme.MarginLarge        // 12f
theme.MarginXLarge       // 20f

// Fine-grained spacing
theme.SpacingTight       // 4
theme.SpacingNormal      // 6
theme.SpacingRelaxed     // 8
```

#### Size Constants
```csharp
// Font & Line
theme.FontSize                // 16
theme.LineHeight              // 20

// Scrollbar
theme.ScrollbarWidth          // 10
theme.ScrollbarPadding        // 4
theme.ScrollbarMinThumbHeight // 20f
theme.ScrollSpeed             // 30 pixels
theme.ScrollWheelSensitivity  // 3 lines

// Controls
theme.ButtonHeight            // 30
theme.InputHeight             // 30
theme.DropdownItemHeight      // 25
theme.PanelRowHeight          // 25
theme.BorderWidth             // 1

// Panel constraints
theme.PanelMaxWidth           // 800f
theme.PanelMaxHeight          // 600f
theme.DropdownMaxHeight       // 300f

// Gaps
theme.ComponentGap            // 10f
theme.PanelEdgeGap            // 20f
theme.TooltipGap              // 5f

// Interaction
theme.InteractiveClickPadding // 10
theme.DragThreshold           // 5f
theme.DoubleClickThreshold    // 0.5f seconds
```

#### Background Colors
```csharp
theme.BackgroundPrimary       // Main background
theme.BackgroundSecondary     // Darker background
theme.BackgroundElevated      // Lighter/elevated surfaces
```

#### Text Colors
```csharp
theme.TextPrimary            // Primary text
theme.TextSecondary          // Secondary text
theme.TextDim                // Dimmed/disabled text
```

#### Status Colors
```csharp
// Success
theme.Success                // Bright success green
theme.SuccessDim             // Dimmed success

// Warning
theme.Warning                // Bright warning
theme.WarningDim             // Dimmed warning
theme.WarningMild            // Mild warning (lighter)

// Error
theme.Error                  // Bright error red
theme.ErrorDim               // Dimmed error

// Info
theme.Info                   // Bright info blue
theme.InfoDim                // Dimmed info
```

#### Interactive Elements
```csharp
// Buttons
theme.ButtonNormal           // Normal state
theme.ButtonHover            // Hover state
theme.ButtonPressed          // Pressed state
theme.ButtonText             // Button text color

// Input
theme.InputBackground        // Input background
theme.InputText              // Input text
theme.InputCursor            // Cursor color
theme.InputSelection         // Selection highlight

// Borders
theme.BorderPrimary          // Default border
theme.BorderFocus            // Focused border

// Scrollbar
theme.ScrollbarTrack         // Scrollbar track
theme.ScrollbarThumb         // Scrollbar thumb
theme.ScrollbarThumbHover    // Hovered thumb
```

#### Special Purpose
```csharp
theme.Prompt                 // Console prompt
theme.Highlight              // Highlighted text
theme.HoverBackground        // Hover state background
theme.CursorLineHighlight    // Current line highlight
```

#### Profiler-Specific
```csharp
theme.ProfilerBarWarningThreshold  // 0.5f (50% of budget)
theme.ProfilerBarMildThreshold     // 0.25f (25% of budget)
theme.ProfilerBarMaxScale          // 2.0f (200% clamp)
theme.ProfilerBarInset             // 2f (bar padding)
theme.ProfilerBudgetLineOpacity    // 0.7f (alpha)
```

### Common Color Patterns

#### Status-based coloring
```csharp
Color GetStatusColor(float value, float warningThreshold, float errorThreshold)
{
    if (value >= errorThreshold) return theme.Error;
    if (value >= warningThreshold) return theme.Warning;
    return theme.Success;
}
```

#### Hover effects
```csharp
if (isHovered)
{
    renderer.DrawRectangle(itemRect, theme.HoverBackground);
}
```

#### Selection highlighting
```csharp
if (isSelected)
{
    renderer.DrawRectangle(itemRect, theme.Info * 0.3f);
}
```

---

## Common Patterns

### Pattern 1: Scrollable List with Empty State

```csharp
private readonly ScrollbarComponent _scrollbar = new();
private List<Item> _items = new();

protected override bool OnHandleInput(UIContext context, InputState input)
{
    if (_items.Count == 0) return false;

    // Mouse wheel scrolling
    bool scrolled = _scrollbar.HandleMouseWheel(input,
        _totalHeight, _visibleHeight);

    // Scrollbar drag/click
    if (ScrollbarComponent.IsNeeded(_totalHeight, _visibleHeight))
    {
        var scrollbarRect = new LayoutRect(
            Rect.Right - Theme.ScrollbarWidth,
            Rect.Y,
            Theme.ScrollbarWidth,
            Rect.Height
        );
        scrolled |= _scrollbar.HandleInput(context, input,
            scrollbarRect, _totalHeight, _visibleHeight, Id);
    }

    return scrolled;
}

protected override void OnRender(UIContext context)
{
    var contentRect = Rect.Shrink(Theme.PaddingMedium);

    // Empty state
    if (_items.Count == 0)
    {
        EmptyStateComponent.DrawNoData(Renderer, Theme, contentRect,
            "No items available",
            "Items will appear here when added");
        return;
    }

    // Calculate dimensions
    int lineHeight = Renderer.GetLineHeight();
    float availableWidth = contentRect.Width;

    if (ScrollbarComponent.IsNeeded(_totalHeight, contentRect.Height))
    {
        availableWidth -= Theme.ScrollbarWidth + Theme.ScrollbarPadding;
    }

    // Draw items
    float y = contentRect.Y - _scrollbar.ScrollOffset;
    foreach (var item in _items)
    {
        if (y + lineHeight < contentRect.Y) continue;  // Above viewport
        if (y > contentRect.Bottom) break;             // Below viewport

        Renderer.DrawText(item.Name, contentRect.X, y, Theme.TextPrimary);
        y += lineHeight + Theme.SpacingTight;
    }

    // Draw scrollbar
    if (ScrollbarComponent.IsNeeded(_totalHeight, contentRect.Height))
    {
        var scrollbarRect = new LayoutRect(
            contentRect.Right - Theme.ScrollbarWidth,
            contentRect.Y,
            Theme.ScrollbarWidth,
            contentRect.Height
        );
        _scrollbar.Draw(Renderer, Theme, scrollbarRect,
            _totalHeight, contentRect.Height);
    }
}
```

### Pattern 2: Sortable Table Header

```csharp
public enum MySort { NameAsc, NameDesc, ValueAsc, ValueDesc }

private readonly SortableTableHeader<MySort> _header;
private MySort _currentSort = MySort.NameAsc;

public MyPanel()
{
    _header = new(MySort.NameAsc);
    _header.SortChanged += sort => {
        _currentSort = sort;
        SortItems();
    };

    _header.AddColumns(
        new() {
            Label = "Name",
            SortMode = MySort.NameAsc,
            X = 10f,
            MaxWidth = 200f,
            Ascending = true
        },
        new() {
            Label = "Name",
            SortMode = MySort.NameDesc,
            X = 10f,
            MaxWidth = 200f,
            Ascending = false
        },
        new() {
            Label = "Value",
            SortMode = MySort.ValueAsc,
            X = 220f,
            Alignment = HorizontalAlignment.Right
        }
    );
}

protected override bool OnHandleInput(UIContext context, InputState input)
{
    return _header.HandleInput(input);
}

protected override void OnRender(UIContext context)
{
    float headerY = contentRect.Y;
    int lineHeight = Renderer.GetLineHeight();

    // Draw sortable header
    _header.DrawWithHover(Renderer, Theme, Input, headerY, lineHeight);

    // Draw sorted items
    float y = headerY + lineHeight + Theme.SpacingNormal;
    foreach (var item in GetSortedItems())
    {
        Renderer.DrawText(item.Name, 10f, y, Theme.TextPrimary);
        Renderer.DrawText(item.Value.ToString(), 220f, y, Theme.TextSecondary);
        y += lineHeight;
    }
}
```

### Pattern 3: Performance Sparklines

```csharp
private readonly Sparkline _frameTimeSparkline;
private readonly Sparkline _fpsSparkline;

public MyPanel()
{
    // Frame time with 16.67ms (60fps) budget
    _frameTimeSparkline = Sparkline.ForFrameTime("frame-time", 60)
        .WithThresholds(warning: 16.67f, error: 33.33f);

    // FPS sparkline
    _fpsSparkline = Sparkline.ForFps("fps", 60);
}

public void Update(GameTime gameTime)
{
    float frameMs = (float)gameTime.ElapsedGameTime.TotalMilliseconds;
    float fps = 1000f / frameMs;

    _frameTimeSparkline.AddValue(frameMs);
    _fpsSparkline.AddValue(fps);
}

protected override void OnRender(UIContext context)
{
    float y = contentRect.Y;
    int lineHeight = Renderer.GetLineHeight();

    // Frame time
    Renderer.DrawText("Frame Time:", contentRect.X, y, Theme.TextPrimary);
    _frameTimeSparkline.Draw(Renderer,
        contentRect.X + 120f, y, 200f, 30f);

    string latestMs = _frameTimeSparkline.LatestValue?.ToString("F2") ?? "--";
    Renderer.DrawText($"{latestMs}ms", contentRect.X + 330f, y, Theme.Info);

    y += 40f;

    // FPS
    Renderer.DrawText("FPS:", contentRect.X, y, Theme.TextPrimary);
    _fpsSparkline.Draw(Renderer,
        contentRect.X + 120f, y, 200f, 30f);

    string latestFps = _fpsSparkline.LatestValue?.ToString("F1") ?? "--";
    Renderer.DrawText(latestFps, contentRect.X + 330f, y, Theme.Info);
}
```

### Pattern 4: Hierarchical Tree with Icons

```csharp
private class TreeNode
{
    public string Name;
    public bool IsExpanded;
    public List<TreeNode> Children = new();
}

protected override void OnRender(UIContext context)
{
    float y = contentRect.Y - _scrollbar.ScrollOffset;
    DrawTree(_rootNode, contentRect.X, ref y, indent: 0);
}

private void DrawTree(TreeNode node, float x, ref float y, int indent)
{
    int lineHeight = Renderer.GetLineHeight();

    // Indentation
    float indentX = x + (indent * 20f);

    // Expand/collapse icon
    string icon = node.Children.Count > 0
        ? (node.IsExpanded ? NerdFontIcons.ExpandedWithSpace : NerdFontIcons.CollapsedWithSpace)
        : "  ";

    // Node icon based on type
    string nodeIcon = node.Children.Count > 0
        ? NerdFontIcons.FolderOpen
        : NerdFontIcons.Entity;

    // Draw
    Renderer.DrawText(icon, indentX, y, Theme.TextSecondary);
    Renderer.DrawText(nodeIcon, indentX + 20f, y, Theme.Info);
    Renderer.DrawText(node.Name, indentX + 40f, y, Theme.TextPrimary);

    y += lineHeight;

    // Children
    if (node.IsExpanded)
    {
        foreach (var child in node.Children)
        {
            DrawTree(child, x, ref y, indent + 1);
        }
    }
}
```

### Pattern 5: Text Truncation in Columns

```csharp
protected override void OnRender(UIContext context)
{
    float nameColumnWidth = PanelConstants.Profiler.NameColumnWidth;
    float valueColumnWidth = 100f;

    foreach (var item in _items)
    {
        // Truncate name to fit column
        string displayName = Renderer.TruncateWithEllipsis(
            item.Name, nameColumnWidth);

        Renderer.DrawText(displayName, x, y, Theme.TextPrimary);

        // Value in next column
        Renderer.DrawText(item.Value.ToString(),
            x + nameColumnWidth + 10f, y, Theme.TextSecondary);

        y += lineHeight;
    }
}
```

---

## Best Practices

### 1. Always Use Theme Constants
❌ **Don't:**
```csharp
float padding = 8f;
Color textColor = new Color(171, 178, 191);
```

✅ **Do:**
```csharp
float padding = theme.PaddingMedium;
Color textColor = theme.TextPrimary;
```

### 2. Use PanelConstants for Layout
❌ **Don't:**
```csharp
float nameWidth = 200f;  // Magic number
float valueWidth = 100f;
```

✅ **Do:**
```csharp
float nameWidth = PanelConstants.Profiler.NameColumnWidth;
float valueWidth = PanelConstants.Profiler.MsColumnWidth;
```

### 3. Handle Empty States
✅ **Always provide feedback when there's no data:**
```csharp
if (_items.Count == 0)
{
    EmptyStateComponent.DrawNoData(renderer, theme, contentRect,
        "No items found", "Items will appear here");
    return;
}
```

### 4. Use Utility Components
✅ **Leverage existing components instead of reinventing:**
```csharp
// Use ScrollbarComponent instead of custom scrolling
private readonly ScrollbarComponent _scrollbar = new();

// Use SortableTableHeader instead of custom sorting UI
private readonly SortableTableHeader<MySort> _header = new(MySort.Default);

// Use TextUtils instead of manual truncation
string text = renderer.TruncateWithEllipsis(longText, maxWidth);
```

### 5. Consistent Iconography
✅ **Use NerdFont icons for consistency:**
```csharp
// Status indicators
string status = isHealthy ? NerdFontIcons.Success : NerdFontIcons.Error;

// Tree navigation
string icon = isExpanded ? NerdFontIcons.Expanded : NerdFontIcons.Collapsed;

// Actions
string playIcon = NerdFontIcons.Play;
string pauseIcon = NerdFontIcons.Pause;
```

### 6. Calculate Scroll Dimensions Correctly
✅ **Account for scrollbar width in layout:**
```csharp
float availableWidth = contentRect.Width;
if (ScrollbarComponent.IsNeeded(totalHeight, contentRect.Height))
{
    availableWidth -= theme.ScrollbarWidth + theme.ScrollbarPadding;
}
```

### 7. Viewport Culling
✅ **Skip rendering items outside viewport:**
```csharp
foreach (var item in items)
{
    if (y + lineHeight < contentRect.Y) continue;  // Above
    if (y > contentRect.Bottom) break;             // Below

    // Draw item
    y += lineHeight;
}
```

---

## Summary

This reference provides all the building blocks for creating consistent, polished panel content:

- **Layout Constants** - Centralized sizing (PanelConstants)
- **Icons** - 100+ NerdFont icons for all scenarios
- **Scrolling** - ScrollbarComponent for consistent behavior
- **Empty States** - EmptyStateComponent for no-data scenarios
- **Table Sorting** - SortableTableHeader for interactive headers
- **Sparklines** - Mini charts for time-series data
- **Text Utils** - Smart truncation with ellipsis
- **Theme System** - Comprehensive color and spacing palette

Use these utilities to build panels that feel native to the debug UI system!
