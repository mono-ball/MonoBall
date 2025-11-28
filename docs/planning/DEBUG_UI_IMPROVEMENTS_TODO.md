# Debug UI Improvements TODO

**Created:** November 28, 2024
**Status:** Phase 1 Complete
**Scope:** `PokeSharp.Engine.Debug` and `PokeSharp.Engine.UI.Debug`

---

## Overview

This document tracks potential improvements to the debug console, panels, and developer tools. Items are prioritized by effort vs. impact.

---

## Priority Legend

| Symbol | Meaning                             |
| ------ | ----------------------------------- |
| ⭐⭐⭐ | High Priority - Implement Soon      |
| ⭐⭐   | Medium Priority - Nice to Have      |
| ⭐     | Low Priority - Future Consideration |
| ✅     | Completed                           |
| 🚧     | In Progress                         |
| 🔲     | Not Started                         |

---

## High-Value Additions

### 1. ✅ Frame Time Graph / Sparklines ⭐⭐⭐ (November 28, 2024)

**Effort:** Medium (2-3 hours)
**Impact:** High
**Location:** `Components/Controls/Sparkline.cs` (reusable component)

Created a reusable `Sparkline` component used by `StatsContent`.

**Implemented:**

- [x] Reusable `Sparkline` UI component with fluent configuration API
- [x] Mini sparkline chart showing frame time history (60 frames)
- [x] Reference line support (e.g., 16.67ms budget line)
- [x] Color-coded thresholds (green = good, yellow = warning, red = error)
- [x] Auto-scaling or fixed min/max
- [x] Factory methods: `ForFrameTime()`, `ForFps()`, `ForPercentage()`
- [x] Both component rendering and inline `Draw()` method
- [x] Used in StatsPanel for frame time history

**Usage Example:**

```csharp
public class Sparkline : UIComponent
{
    private readonly float[] _history = new float[120];
    private int _historyIndex = 0;
    private float _minValue, _maxValue;

    public void AddSample(float value) { /* circular buffer */ }
    protected override void OnRender(UIContext context) { /* draw polyline */ }
}
```

**Integration:**

- Add to `StatsPanel` or create dedicated `PerformanceGraphPanel`
- Pull data from existing `PerformanceMonitor`

---

### 2. ✅ System Profiler Panel — IMPLEMENTED

**Status:** Complete (November 28, 2024)
**Location:** `Components/Debug/ProfilerPanel.cs`, `ProfilerPanelBuilder.cs`

Surface `SystemPerformanceTracker` data in a visual panel.

**Features:**

- [x] Horizontal bar chart of per-system execution times
- [x] Highlight systems exceeding frame budget (>2ms) with color coding
- [x] Sort by: execution time, average, max, name
- [x] Show frame budget line (16.67ms for 60fps)
- [x] Percentage of frame budget consumed
- [x] Status bar with total frame time and system count
- [x] Auto-refresh at configurable interval

**Console Command:**

```
profiler                   Show profiler statistics
profiler sort <mode>       Set sort mode (time, avg, max, name)
profiler active <on|off>   Show only active systems
profiler refresh           Force refresh metrics
profiler system <name>     Show detailed metrics for a system
profiler list              List all tracked systems
```

**Access:** Ctrl+6 to open Profiler tab

**Implementation:**

- `ProfilerPanel` renders horizontal bars with color-coded thresholds
- `ProfilerPanelBuilder` for fluent configuration
- `IProfilerOperations` interface for command access
- `ProfilerCommand` for console control
- Metrics provider callback wired up in `ConsoleSystem.HandleConsoleReady()`
- Added as 6th tab in `ConsoleTabs` (Ctrl+6)

---

### 3. ✅ Time Control Commands — IMPLEMENTED

**Status:** Complete (November 28, 2024)
**Location:** `Commands/BuiltIn/TimeCommand.cs`

Game speed manipulation for debugging.

**Commands:**

```
time                  Show current time state
time pause            Pause the game (timescale = 0)
time resume           Resume the game (timescale = 1)
time step [frames]    Step forward N frames when paused
time scale <value>    Set time scale (0.5, 1, 2, etc.)
time slowmo <percent> Set slow motion (25 = 25% speed)

Shortcuts:
  pause               Pause the game
  resume              Resume the game
  step [frames]       Step frames when paused
```

**Implementation:**

- Extended `IGameTimeService` with `TimeScale`, `IsPaused`, `UnscaledDeltaTime`, `Step()`, `Pause()`, `Resume()`
- `GameTimeService` applies time scale to `DeltaTime`, preserving raw `UnscaledDeltaTime`
- `GameplayScene.Update()` uses unscaled time for input (controls work when paused)
- `ConsoleTimeCallbacks` bridges console commands to `IGameTimeService`
- Uses `CultureInfo.InvariantCulture` for parsing decimal values (e.g., `time scale 0.5`)
- Direct project reference from `Engine.Debug` to `Game.Systems` for reliable service resolution

---

### 4. 🔲 State Snapshots ⭐⭐

**Effort:** High (6-8 hours)
**Impact:** High
**Location:** New `SnapshotCommand.cs` + `SnapshotManager.cs`

Save and compare game state for debugging.

**Commands:**

```
snapshot save <name>     Capture current state
snapshot load <name>     Restore saved state
snapshot list            List all snapshots
snapshot delete <name>   Delete a snapshot
snapshot diff <name>     Compare current vs saved (show changes)
snapshot export <name>   Export to JSON file
```

**Implementation Notes:**

- Serialize ECS world state (entities + components)
- Store player position, inventory, game flags
- Diff algorithm to highlight changes
- Consider using JSON or binary serialization

---

### 5. 🔲 Watch Graphing ⭐⭐

**Effort:** Medium (2-3 hours)
**Impact:** Medium
**Location:** Extend `WatchPanel`

Add mini-graphs for numeric watch values.

**Features:**

- [ ] Toggle graph mode per watch: `watch graph <name> on/off`
- [ ] Show trend line for values with history
- [ ] Highlight when values spike (> 2 std deviations)
- [ ] Configurable graph width/height
- [ ] Auto-scale Y axis

**Command:**

```
watch add fps () => Performance.Fps --graph
watch graph fps on
watch graph fps off
```

---

## Medium-Effort Improvements

### 6. ✅ Console History Search (Ctrl+R) — ALREADY IMPLEMENTED

**Status:** Complete
**Location:** `ConsolePanel.cs`

Already implemented! Press `Ctrl+R` to toggle command history search mode.

**Features:**

- `Ctrl+R` toggles history search overlay
- Type to filter history
- Up/Down to navigate matches
- Enter to select, Escape to cancel
- Uses `ConsoleOverlayMode.CommandHistorySearch`

---

### 7. ✅ Breakpoint / Conditional Pause — IMPLEMENTED

**Status:** Complete (November 28, 2024)
**Location:** `Breakpoints/` folder, `Commands/BuiltIn/BreakpointCommand.cs`

Pause game when conditions are met.

**Commands:**

```
break                          List all breakpoints
break when <expression>        Pause when C# expression becomes true
break log <level>              Pause on log level (error, warning, info)
break watch <name>             Pause when watch alert triggers
break enable <id>              Enable a breakpoint
break disable <id>             Disable a breakpoint
break delete <id>              Delete a breakpoint
break clear                    Delete all breakpoints
break toggle                   Toggle breakpoint evaluation on/off
```

**Implementation:**

- `BreakpointManager` - Core class managing all breakpoints
- `IBreakpoint` interface with multiple implementations:
  - `ExpressionBreakpoint` - Evaluates C# expressions (triggers on false→true transition)
  - `LogLevelBreakpoint` - Triggers on specific log levels
  - `WatchAlertBreakpoint` - Triggers on watch alerts
- Integrated with time control (auto-pauses game)
- Console opens automatically when breakpoint hits
- Added `IsAlertActive(name)` to IWatchOperations for watch breakpoints
- Breakpoints evaluated every frame in ConsoleSystem.Update()

---

### 8. 🔲 Input State Panel ⭐⭐

**Effort:** Medium (2-3 hours)
**Impact:** Medium
**Location:** New `InputPanel` in `Components/Debug/`

Show current input state in real-time.

**Features:**

- [ ] Keyboard: highlight currently pressed keys
- [ ] Controller: show axes (sticks) as visual joysticks, buttons highlighted
- [ ] Input buffer: last N inputs with timestamps
- [ ] Input mapping: show which actions are bound to which keys
- [ ] Mouse position and button states

**Use Cases:**

- Debug "why isn't my input working?"
- Verify input mappings
- Test controller support

---

### 9. 🔲 Event Log Panel ⭐⭐

**Effort:** Medium (3-4 hours)
**Impact:** Medium
**Location:** New `EventsPanel` in `Components/Debug/`

Track game events (separate from system logs).

**Event Types:**

```
[12:34:56.123] EntitySpawned: Player (ID: 42)
[12:34:56.456] CollisionEnter: Player -> Wall
[12:34:56.789] ComponentAdded: Health to Entity 42
[12:34:57.012] SceneLoaded: GameplayScene
[12:34:57.345] ItemPickup: Potion by Player
```

**Features:**

- [ ] Filter by event type
- [ ] Filter by entity ID
- [ ] Timestamp toggle
- [ ] Pause/resume event capture
- [ ] Event rate indicator (events/sec)

**Implementation:**

- Create `IGameEvent` interface
- `EventBus` or observer pattern for game events
- Panel subscribes and displays

---

### 10. ✅ Console Macros Enhancement — IMPLEMENTED

**Status:** Complete (November 28, 2024)
**Location:** `ConsoleSystem.cs` - `ExecuteConsoleCommand()`, `SplitChainedCommands()`, `IsBuiltInCommand()`

**Features:**

- Command chaining with `;` separator
- Respects quoted strings (`;` inside quotes not treated as separator)
- Works with aliases (expanded aliases can contain `;` chains)
- Built-in commands bypass C# multi-line syntax checking
- Typos get proper "command not found" errors instead of entering multi-line mode

**Commands:**

```
# Multi-command chain:
clear; help; time

# Multi-command alias:
alias spawn_test=entity spawn NPC; entity spawn Player; time pause

# Run it:
spawn_test
```

**Implementation:**

- ✅ Parse `;` in command executor to run sequential commands
- ✅ Quoted strings protected from splitting
- ✅ Alias expansion supports chained commands
- ✅ `IsBuiltInCommand()` detects registered commands and aliases
- ✅ `LooksLikeCommand()` heuristic prevents command-like typos from entering C# multi-line mode
- ✅ Decimal parsing uses `CultureInfo.InvariantCulture` for locale-independent behavior

**Note:** Parameter substitution (`$1`, `$2`) already works in aliases.

---

## Polish Improvements

### 11. 🔲 Script Snippets Library ⭐

**Effort:** Medium (2-3 hours)
**Impact:** Low-Medium
**Location:** New `SnippetCommand.cs` + snippets storage

Save and quickly run common C# script sequences.

**Commands:**

```
snippet save <name> "<code>"
snippet run <name>
snippet list
snippet delete <name>
snippet edit <name>
snippet export <file>
snippet import <file>
```

**Example:**

```
snippet save heal_all "foreach(var e in World.Query<Health>()) e.Get<Health>().Current = 100"
snippet run heal_all
```

---

### 12. 🔲 Collision Debug Visualization ⭐

**Effort:** Medium (3-4 hours)
**Impact:** Medium
**Location:** New `CollisionDebugSystem` or command

Toggle collision hitbox overlays.

**Commands:**

```
debug collision on           Show all hitboxes
debug collision off          Hide hitboxes
debug collision layer <n>    Show specific collision layer
debug collision entity <id>  Show hitbox for specific entity
```

**Rendering:**

- Draw rectangles/shapes for collision bounds
- Color-code by collision layer
- Show collision normals on contact

---

### 13. ✅ Enhanced Stats Panel ⭐ (November 28, 2024)

**Effort:** Medium (2-3 hours)
**Impact:** Medium
**Location:** Upgrade existing `StatsPanel`

Improved the basic stats display with comprehensive performance visualization.

**Implemented:**

- [x] Clean layout matching ProfilerPanel style (horizontal separators, no row backgrounds)
- [x] Frame budget progress bar with 16.67ms marker and color coding
- [x] Frame time sparkline (60-frame history)
- [x] Memory usage with progress bar and thresholds
- [x] GC statistics (Gen0/Gen1/Gen2) inline with delta rates per second
- [x] Entity and system counts
- [x] Status bar with overall health indicator (✓/●/⚠)
- [x] `stats` command with subcommands (fps, frame, memory, gc, summary, show)
- [x] IStatsOperations interface for command access
- [x] Stats tab (Ctrl+7) in console
- [x] Theme-compatible colors (works on light and dark themes)
- [ ] Expandable sections

---

### 14. ⏸️ Entity Hierarchy View ⭐ (DEFERRED)

**Effort:** High (4-6 hours)
**Impact:** Low-Medium
**Location:** Extend `EntitiesPanel` or new panel

**Status:** Deferred - Game doesn't currently use parent-child entity relationships.
Would display empty results. Implement when hierarchy components are added to the ECS.

---

### 15. 🔲 Console Output Categories ⭐

**Effort:** Low (1-2 hours)
**Impact:** Low
**Location:** `ConsolePanel` / `TextBuffer`

Add collapsible categories in console output.

**Display:**

```
▼ [System Init] (15 messages)
  Loaded configuration...
  Initialized graphics...
▼ [Scripts] (3 messages)
▶ [Debug] (collapsed - 142 messages)
```

**Implementation:**

- Track output by category/tag
- Collapsible sections in TextBuffer
- Filter by category

---

## Implementation Priority Matrix

| Feature                    | Effort     | Impact     | Priority | Depends On          |
| -------------------------- | ---------- | ---------- | -------- | ------------------- |
| ~~Time Control Commands~~  | ~~Low~~    | ~~High~~   | ✅ Done  | -                   |
| ~~Frame Time Sparklines~~  | ~~Medium~~ | ~~High~~   | ✅ Done  | -                   |
| ~~System Profiler Panel~~  | ~~Medium~~ | ~~High~~   | ✅ Done  | -                   |
| ~~Console History Search~~ | ~~Low~~    | ~~Medium~~ | ✅ Done  | -                   |
| Watch Graphing             | Medium     | Medium     | ⭐⭐     | ~~Sparklines~~ ✅   |
| State Snapshots            | High       | High       | ⭐⭐     | -                   |
| ~~Breakpoints~~            | ~~High~~   | ~~High~~   | ✅ Done  | ~~Time Control~~ ✅ |
| Input State Panel          | Medium     | Medium     | ⭐⭐     | -                   |
| Event Log Panel            | Medium     | Medium     | ⭐⭐     | -                   |
| ~~Macros Enhancement~~     | ~~Low~~    | ~~Medium~~ | ✅ Done  | -                   |
| Script Snippets            | Medium     | Low        | ⭐       | -                   |
| Collision Debug            | Medium     | Medium     | ⭐       | -                   |
| ~~Enhanced Stats~~         | ~~Medium~~ | ~~Medium~~ | ✅ Done  | ~~Sparklines~~ ✅   |
| Entity Hierarchy           | High       | Low        | ⏸️       | Deferred (no data)  |
| Output Categories          | Low        | Low        | ⭐       | -                   |

---

## Suggested Implementation Order

### Phase 1: Quick Wins (1-2 days) ✅ COMPLETE

1. ✅ Time Control Commands (November 28, 2024)
2. ✅ Console History Search (already implemented!)
3. ✅ Command Chaining / Macros Enhancement (November 28, 2024)

### Phase 2: Visual Debugging (2-3 days) — IN PROGRESS

4. ✅ Frame Time Sparklines (reusable component) (November 28, 2024)
5. ✅ System Profiler Panel (November 28, 2024)
6. 🔲 Watch Graphing

### Phase 3: Advanced Features (3-5 days) — IN PROGRESS

7. ✅ Breakpoints (November 28, 2024)
8. 🔲 State Snapshots
9. 🔲 Input State Panel
10. 🔲 Event Log Panel

### Phase 4: Polish (ongoing)

11. 🔲 Collision Debug Visualization
12. ✅ Enhanced Stats Panel (November 28, 2024)
13. ⏸️ Entity Hierarchy View (deferred - no hierarchy data)
14. 🔲 Script Snippets
15. 🔲 Output Categories

---

## Notes

- All new panels should use the existing builder pattern (see `ConsolePanelBuilder`)
- New commands should follow the `IConsoleCommand` interface
- Consider keyboard shortcuts for frequently used features
- Test with both light and dark themes
- Document new commands in `scripts/API_REFERENCE.md`

---

## Changelog

### November 28, 2024

**Phase 1 Completed:**

- ✅ **Time Control Commands** - Full implementation with `time`, `pause`, `resume`, `step`, `scale`, `slowmo`
- ✅ **Command Chaining** - Multi-command execution with `;` separator
- ✅ **Console History Search** - Already existed (Ctrl+R)

**Phase 2 Progress:**

- ✅ **System Profiler Panel** - New tab showing per-system performance with color-coded horizontal bars
  - Added `ProfilerPanel`, `ProfilerPanelBuilder`, `IProfilerOperations`
  - Added `ProfilerCommand` for console control
  - Accessible via Ctrl+6

**Phase 3 Progress:**

- ✅ **Breakpoints / Conditional Pause** - Pause game when conditions are met
  - Expression breakpoints: `break when Player.Health < 20`
  - Log level breakpoints: `break log error`
  - Watch alert breakpoints: `break watch playerPos`
  - Console opens automatically when breakpoint hits
  - Breakpoints evaluated every frame

**Phase 4 Progress:**

- ✅ **Enhanced Stats Panel** - Comprehensive performance visualization
  - Clean layout matching ProfilerPanel style (no row highlighting)
  - Frame budget progress bar with color coding and 16.67ms marker
  - Frame time sparkline showing 60-frame history
  - Memory usage with progress bar and thresholds
  - GC statistics (Gen0/Gen1/Gen2) inline with delta rates (`+X/Y/Z/s`)
  - Entity and system counts
  - Horizontal separators between sections
  - Status bar with overall health indicator (✓/●/⚠)
  - `stats` command: `stats fps`, `stats frame`, `stats memory`, `stats gc`, `stats show`
  - Accessible via Ctrl+7

**Phase 2 Progress (continued):**

- ✅ **Frame Time Sparklines** - Extracted to reusable `Sparkline` component
  - `Sparkline.cs` in Controls folder
  - Supports configurable thresholds, reference lines, auto-scaling
  - Factory methods: `ForFrameTime()`, `ForFps()`, `ForPercentage()`
  - Both component-based rendering and inline `Draw()` method
  - StatsContent now uses the reusable Sparkline component

**UI Polish:**

- Fixed StatusBar alignment in Stats/Profiler panels to match Watch/Logs panels
- Improved theme compatibility - removed row highlighting that clashed with themes
- Consistent `Constraint.Padding` usage across all debug panels

**Bug Fixes:**

- Fixed decimal parsing (`time scale 0.5`) using `CultureInfo.InvariantCulture`
- Fixed built-in commands triggering C# multi-line mode
- Added `LooksLikeCommand()` heuristic so typos show proper errors instead of entering multi-line mode
- Added `IsBuiltInCommand()` check to bypass Roslyn syntax validation for console commands
- Fixed breakpoint example expressions (removed non-existent `frameCount`, now shows valid globals)

---

_Last Updated: November 28, 2024_
