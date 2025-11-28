# Time Control Architecture Fix Plan

**Created:** November 28, 2024
**Status:** ✅ COMPLETE
**Branch:** `debug-ui`

---

## Problem Summary

The time control implementation introduced an architectural layer violation where `PokeSharp.Engine.Debug` (a generic engine module) depends on `PokeSharp.Game.Systems` (game-specific code). This breaks the principle that engine code should be reusable across different games.

---

## Current Architecture (Problematic)

```
┌─────────────────────────────────────────────────────────────────┐
│                         GAME LAYER                               │
├─────────────────────────────────────────────────────────────────┤
│  PokeSharp.Game                                                  │
│    └── GameplayScene (uses IGameTimeService)                    │
│                                                                  │
│  PokeSharp.Game.Systems                                         │
│    ├── IGameTimeService  ◄── INTERFACE DEFINED HERE             │
│    └── GameTimeService   ◄── IMPLEMENTATION HERE                │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ PROBLEMATIC REFERENCE
                              │
┌─────────────────────────────┼───────────────────────────────────┐
│                         ENGINE LAYER                             │
├─────────────────────────────────────────────────────────────────┤
│  PokeSharp.Engine.Debug                                         │
│    ├── ConsoleSystem (creates ConsoleTimeCallbacks)             │
│    ├── ConsoleContext (implements IConsoleTimeControl)          │
│    └── TimeCommand (uses IConsoleTimeControl)                   │
│                                                                  │
│  PokeSharp.Engine.Debug/Commands/Interfaces/                    │
│    └── IConsoleTimeControl  ◄── DUPLICATE INTERFACE             │
└─────────────────────────────────────────────────────────────────┘
```

**Problems:**
1. `Engine.Debug` references `Game.Systems` (layer violation)
2. `IConsoleTimeControl` duplicates `IGameTimeService` operations
3. `ConsoleTimeCallbacks` wraps everything in delegates (unnecessary indirection)

---

## Target Architecture (Fixed)

```
┌─────────────────────────────────────────────────────────────────┐
│                         GAME LAYER                               │
├─────────────────────────────────────────────────────────────────┤
│  PokeSharp.Game                                                  │
│    └── GameplayScene (uses IGameTimeService)                    │
│                                                                  │
│  PokeSharp.Game.Systems                                         │
│    ├── IGameTimeService : ITimeControl  ◄── EXTENDS ENGINE      │
│    └── GameTimeService                                          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ DI INJECTION (via ITimeControl)
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                         ENGINE LAYER                             │
├─────────────────────────────────────────────────────────────────┤
│  PokeSharp.Engine.Core (or Engine.Systems)                      │
│    └── ITimeControl  ◄── SHARED INTERFACE                       │
│          ├── TimeScale { get; set; }                            │
│          ├── IsPaused { get; }                                  │
│          ├── Pause()                                            │
│          ├── Resume()                                           │
│          ├── Step(int frames)                                   │
│          └── OnTimeScaleChanged event                           │
│                                                                  │
│  PokeSharp.Engine.Debug                                         │
│    ├── ConsoleSystem (receives ITimeControl? via DI)            │
│    ├── ConsoleContext (holds ITimeControl?)                     │
│    └── TimeCommand (uses context.TimeControl)                   │
│                                                                  │
│  (IConsoleTimeControl REMOVED - use ITimeControl directly)      │
└─────────────────────────────────────────────────────────────────┘
```

**Benefits:**
1. No layer violations - Engine doesn't reference Game
2. Single interface definition - no duplication
3. Direct interface usage - no callback indirection
4. Optional dependency - graceful when `ITimeControl` not registered

---

## Implementation Plan

### Phase 1: Create Shared Interface in Engine Layer

**Files to create:**
- `PokeSharp.Engine.Core/Services/ITimeControl.cs`

**Contents:**
```csharp
namespace PokeSharp.Engine.Core.Services;

/// <summary>
/// Provides time control operations for debugging and development.
/// Allows pausing, stepping, and time scaling.
/// </summary>
public interface ITimeControl
{
    /// <summary>
    /// Gets or sets the time scale multiplier.
    /// 1.0 = normal, 0.5 = half speed, 2.0 = double, 0 = paused.
    /// </summary>
    float TimeScale { get; set; }

    /// <summary>
    /// Gets whether time is paused (TimeScale == 0).
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    /// Pauses time (sets TimeScale to 0).
    /// </summary>
    void Pause();

    /// <summary>
    /// Resumes time at previous or normal speed.
    /// </summary>
    void Resume();

    /// <summary>
    /// Steps forward by specified frames when paused.
    /// </summary>
    void Step(int frames = 1);

    /// <summary>
    /// Raised when time scale changes (including pause/resume).
    /// </summary>
    event Action<float>? OnTimeScaleChanged;
}
```

### Phase 2: Update Game.Systems to Extend Engine Interface

**Files to modify:**
- `PokeSharp.Game.Systems/Services/IGameTimeService.cs`
- `PokeSharp.Game.Systems/Services/GameTimeService.cs`

**Changes:**
```csharp
// IGameTimeService.cs
using PokeSharp.Engine.Core.Services;

public interface IGameTimeService : ITimeControl
{
    // Game-specific properties (keep existing)
    float TotalSeconds { get; }
    double TotalMilliseconds { get; }
    float DeltaTime { get; }
    float UnscaledDeltaTime { get; }
    int StepFrames { get; set; }

    void Update(float totalSeconds, float deltaTime);

    // ITimeControl members inherited:
    // TimeScale, IsPaused, Pause(), Resume(), Step(), OnTimeScaleChanged
}
```

```csharp
// GameTimeService.cs - add event implementation
public event Action<float>? OnTimeScaleChanged;

public float TimeScale
{
    get => _timeScale;
    set
    {
        var oldValue = _timeScale;
        _timeScale = Math.Clamp(value, 0f, 10f);
        if (_timeScale > 0) _previousTimeScale = _timeScale;

        if (Math.Abs(oldValue - _timeScale) > 0.001f)
            OnTimeScaleChanged?.Invoke(_timeScale);
    }
}
```

### Phase 3: Remove Layer Violation from Engine.Debug

**Files to modify:**
- `PokeSharp.Engine.Debug/PokeSharp.Engine.Debug.csproj` - Remove Game.Systems reference
- `PokeSharp.Engine.Debug/Commands/ConsoleServices.cs` - Remove `ConsoleTimeCallbacks`
- `PokeSharp.Engine.Debug/Commands/ConsoleContext.cs` - Use `ITimeControl?` directly
- `PokeSharp.Engine.Debug/Commands/IConsoleContext.cs` - Remove `IConsoleTimeControl` inheritance
- `PokeSharp.Engine.Debug/Systems/ConsoleSystem.cs` - Get `ITimeControl` from DI

**Files to delete:**
- `PokeSharp.Engine.Debug/Commands/Interfaces/IConsoleTimeControl.cs`

### Phase 4: Simplify ConsoleContext

**Before:**
```csharp
public class ConsoleContext : IConsoleContext  // IConsoleContext : ... : IConsoleTimeControl
{
    private readonly ConsoleTimeCallbacks _timeCallbacks;

    public bool IsTimeControlAvailable => _timeCallbacks.IsAvailable;
    public float TimeScale
    {
        get => _timeCallbacks.GetTimeScale();
        set => _timeCallbacks.SetTimeScale(value);
    }
    // ... more delegation
}
```

**After:**
```csharp
public class ConsoleContext : IConsoleContext
{
    private readonly ITimeControl? _timeControl;

    public ITimeControl? TimeControl => _timeControl;

    // Commands access: context.TimeControl?.Pause()
}
```

### Phase 5: Update TimeCommand

**Changes:**
```csharp
public Task ExecuteAsync(IConsoleContext context, string[] args)
{
    var timeControl = context.TimeControl;

    if (timeControl == null)
    {
        context.WriteLine("⚠ Time control is not available.", theme.Warning);
        return Task.CompletedTask;
    }

    // Use timeControl directly instead of context
    timeControl.Pause();
    timeControl.Resume();
    timeControl.TimeScale = 0.5f;
    // etc.
}
```

### Phase 6: Fix Behavioral Issues

1. **Add pause check to `time step`:**
```csharp
case "step":
    if (!timeControl.IsPaused)
    {
        context.WriteLine("Game is not paused. Use 'pause' first.", theme.Warning);
        break;
    }
    // ... existing step code
```

2. **Add thread safety to TimeScale:**
```csharp
private volatile float _timeScale = 1.0f;
// Or use Interlocked for atomic operations
```

3. **Document Step behavior (assignment, not accumulation):**
```csharp
/// <summary>
/// Steps forward by specified frames when paused.
/// Note: Calling Step() replaces any pending steps, it does not accumulate.
/// </summary>
```

---

## Files Changed Summary

### New Files
| File | Description |
|------|-------------|
| `PokeSharp.Engine.Core/Services/ITimeControl.cs` | Shared time control interface |

### Modified Files
| File | Changes |
|------|---------|
| `PokeSharp.Engine.Core/PokeSharp.Engine.Core.csproj` | Add Services folder |
| `PokeSharp.Game.Systems/Services/IGameTimeService.cs` | Extend ITimeControl |
| `PokeSharp.Game.Systems/Services/GameTimeService.cs` | Add event, thread safety |
| `PokeSharp.Game.Systems/PokeSharp.Game.Systems.csproj` | Add Engine.Core reference |
| `PokeSharp.Engine.Debug/PokeSharp.Engine.Debug.csproj` | Remove Game.Systems reference |
| `PokeSharp.Engine.Debug/Commands/ConsoleServices.cs` | Remove ConsoleTimeCallbacks |
| `PokeSharp.Engine.Debug/Commands/ConsoleContext.cs` | Use ITimeControl? directly |
| `PokeSharp.Engine.Debug/Commands/IConsoleContext.cs` | Expose TimeControl property |
| `PokeSharp.Engine.Debug/Systems/ConsoleSystem.cs` | Get ITimeControl from DI |
| `PokeSharp.Engine.Debug/Commands/BuiltIn/TimeCommand.cs` | Use TimeControl, add pause check |

### Deleted Files
| File | Reason |
|------|--------|
| `PokeSharp.Engine.Debug/Commands/Interfaces/IConsoleTimeControl.cs` | Replaced by shared ITimeControl |

---

## Dependency Graph After Fix

```
PokeSharp.Game
  └── PokeSharp.Game.Systems
        └── PokeSharp.Engine.Core (for ITimeControl)

PokeSharp.Engine.Debug
  ├── PokeSharp.Engine.Core (for ITimeControl)
  ├── PokeSharp.Engine.UI.Debug
  └── PokeSharp.Game.Scripting

NO CROSS-LAYER VIOLATIONS ✓
```

---

## Testing Checklist

- [ ] `time` command shows status
- [ ] `time pause` pauses game (entities stop moving)
- [ ] `time resume` resumes at previous speed
- [ ] `time step` works only when paused (shows warning if not)
- [ ] `time step 60` advances ~1 second
- [ ] `time scale 0.5` gives slow motion
- [ ] `time scale 2` gives fast forward
- [ ] `pause`, `resume`, `step` shortcut commands work
- [ ] Console UI still responsive when game paused
- [ ] Input works when paused (for console interaction)
- [ ] Time control unavailable message shows gracefully when service missing

---

## Risk Assessment

| Risk | Mitigation |
|------|------------|
| Breaking existing command behavior | Maintain same command syntax and output |
| Missing DI registration | Graceful null handling, helpful error messages |
| Thread safety issues | Use volatile/Interlocked for shared state |
| Event handler leaks | Use weak events or ensure proper unsubscription |

---

## Estimated Effort

| Phase | Time |
|-------|------|
| Phase 1: Create ITimeControl | 15 min |
| Phase 2: Update Game.Systems | 20 min |
| Phase 3: Remove layer violation | 30 min |
| Phase 4: Simplify ConsoleContext | 20 min |
| Phase 5: Update TimeCommand | 15 min |
| Phase 6: Fix behavioral issues | 15 min |
| Testing | 20 min |
| **Total** | **~2.5 hours** |

---

## Implementation Summary (Completed)

All phases were successfully implemented:

### Files Created
- ✅ `PokeSharp.Engine.Core/Services/ITimeControl.cs` - Shared time control interface

### Files Modified
- ✅ `PokeSharp.Game.Systems/Services/IGameTimeService.cs` - Now extends `ITimeControl`
- ✅ `PokeSharp.Game.Systems/Services/GameTimeService.cs` - Added event, thread safety
- ✅ `PokeSharp.Engine.Debug/PokeSharp.Engine.Debug.csproj` - Removed Game.Systems reference
- ✅ `PokeSharp.Engine.Debug/Commands/ConsoleServices.cs` - Removed `ConsoleTimeCallbacks`
- ✅ `PokeSharp.Engine.Debug/Commands/ConsoleContext.cs` - Uses `ITimeControl?` directly
- ✅ `PokeSharp.Engine.Debug/Commands/IConsoleContext.cs` - Exposes `TimeControl` property
- ✅ `PokeSharp.Engine.Debug/Systems/ConsoleSystem.cs` - Gets `ITimeControl` from DI
- ✅ `PokeSharp.Engine.Debug/Commands/BuiltIn/TimeCommand.cs` - Uses `context.TimeControl`, added pause check
- ✅ `PokeSharp.Game/Infrastructure/ServiceRegistration/GameServicesExtensions.cs` - Registers both interfaces

### Files Deleted
- ✅ `PokeSharp.Engine.Debug/Commands/Interfaces/IConsoleTimeControl.cs` - Replaced by shared interface

### Key Improvements
1. **No layer violations** - Engine.Debug no longer references Game.Systems
2. **Single interface** - `ITimeControl` defined once in Engine.Core
3. **Direct injection** - No callback indirection, uses `ITimeControl?` directly
4. **Thread safety** - `volatile` on `TimeScale` for cross-thread access
5. **Event notifications** - `OnTimeScaleChanged` event for observers
6. **Consistent validation** - Both `time step` and `step` commands check for paused state
7. **Documented behavior** - `Step()` replacement (not accumulation) documented in code

---

_Last Updated: November 28, 2024_

