# Event Inspector Manual Testing Guide

**Version**: 1.0
**Date**: December 3, 2025
**Prerequisites**: Event Inspector integration must be completed first

---

## Prerequisites

1. ✅ Event Inspector integration completed in GameplayScene
2. ✅ InputManager F9 handler implemented
3. ✅ Project builds successfully: `dotnet build PokeSharp.sln`
4. ✅ EventBus is registered and functional

---

## Setup Instructions

### 1. Build Project
```bash
cd /Users/ntomsic/Documents/PokeSharp
dotnet build PokeSharp.sln
```

**Expected**: Build succeeds with 0 errors, 0 warnings

---

### 2. Run Game
```bash
dotnet run --project PokeSharp.Game
```

**Expected**: Game starts and loads into gameplay scene

---

## Test Cases

### TC1: Enable Event Inspector
**Objective**: Verify Event Inspector can be toggled on

**Steps**:
1. Game is running in gameplay scene
2. Press **F9** key

**Expected Results**:
- ✅ Event Inspector panel appears on screen
- ✅ Panel displays in upper portion of screen (800x600 area)
- ✅ Console log shows: `"Event Inspector: ON"`
- ✅ Panel shows "Event Inspector" header
- ✅ Panel contains sections: Events, Recent Activity, Subscriptions

**Pass Criteria**:
- Panel is visible
- No null reference exceptions
- Log message appears

**Failure Actions**:
- Check console for errors
- Verify F9 handler in InputManager
- Verify panel initialization in GameplayScene

---

### TC2: View Event Activity (Static State)
**Objective**: Verify Event Inspector displays events when player is idle

**Steps**:
1. Enable Event Inspector (F9)
2. Let player stand still for 3-5 seconds
3. Observe Event Inspector panel

**Expected Results**:
- ✅ Panel displays registered event types
- ✅ Subscriber counts shown for each event
- ✅ Event list includes system events (e.g., `FrameUpdateEvent`, `RenderEvent`)
- ✅ No events showing 0 subscribers
- ✅ Performance metrics displayed (avg time, max time)

**Pass Criteria**:
- At least 3-5 event types visible
- Subscriber counts > 0
- No crashes or freezes

**Failure Actions**:
- Verify EventBus.Metrics is set
- Check adapter initialization
- Verify event handlers are subscribed

---

### TC3: View Event Activity (Movement)
**Objective**: Verify Event Inspector captures movement events

**Steps**:
1. Enable Event Inspector (F9)
2. Move player character using arrow keys (up, down, left, right)
3. Move for ~10 seconds in different directions
4. Observe Event Inspector panel

**Expected Results**:
- ✅ "Recent Events" section updates in real-time
- ✅ Movement-related events appear:
  - `MovementStartedEvent`
  - `MovementCompletedEvent`
  - `PositionChangedEvent` (if implemented)
- ✅ Event timestamps show recent activity (within last 2-3 seconds)
- ✅ Event publish counts increase
- ✅ Performance metrics update (avg time, max time)

**Pass Criteria**:
- Recent events scroll/update
- Movement events clearly visible
- Metrics reflect actual activity

**Failure Actions**:
- Verify metrics collection is enabled
- Check if ResetTimings() is called on enable
- Verify refresh interval is set (2 frames)

---

### TC4: Disable Event Inspector
**Objective**: Verify Event Inspector can be toggled off

**Steps**:
1. Event Inspector is currently enabled
2. Press **F9** key again

**Expected Results**:
- ✅ Event Inspector panel disappears from screen
- ✅ Console log shows: `"Event Inspector: OFF"`
- ✅ Game continues running normally
- ✅ FPS returns to baseline (if any impact)

**Pass Criteria**:
- Panel is no longer visible
- No errors or crashes
- Log message appears

**Failure Actions**:
- Check toggle logic in InputManager
- Verify panel.Visible is set to false
- Check adapter.IsEnabled is set to false

---

### TC5: Performance Overhead (Enabled State)
**Objective**: Verify Event Inspector overhead is acceptable

**Steps**:
1. Enable Performance Overlay (F3)
2. Note baseline FPS with Inspector disabled
3. Enable Event Inspector (F9)
4. Move player continuously for 30 seconds
5. Compare FPS

**Expected Results**:
- ✅ FPS remains at 60 (or close to target FPS)
- ✅ FPS drop is <5% (e.g., 60 → 57 FPS is acceptable)
- ✅ No frame stuttering or lag
- ✅ CPU usage increase is minimal (<5%)

**Performance Targets** (from documentation):
- Event Publish: <1μs
- Handler Invoke: <0.5μs per handler
- Frame Overhead: <0.5ms (2-5% of 16.67ms frame budget at 60 FPS)

**Pass Criteria**:
- FPS drop < 3 frames
- No visible lag or stuttering
- Game remains playable

**Failure Actions**:
- Check refresh interval (should be 2, not 1)
- Verify metrics collection is efficient
- Consider increasing refresh interval to 5

---

### TC6: Simultaneous Debug Panels (F3 + F9)
**Objective**: Verify Event Inspector doesn't conflict with Performance Overlay

**Steps**:
1. Enable Performance Overlay (F3)
2. Enable Event Inspector (F9)
3. Verify both panels are visible
4. Move player and interact with game
5. Disable Performance Overlay (F3)
6. Disable Event Inspector (F9)

**Expected Results**:
- ✅ Both panels visible simultaneously
- ✅ Panels don't overlap or obscure each other
- ✅ Performance Overlay: top-left corner
- ✅ Event Inspector: positioned separately
- ✅ No input conflicts (both F3 and F9 work)
- ✅ Game performance remains stable with both enabled

**Pass Criteria**:
- Both panels functional
- No visual conflicts
- Independent toggle behavior

**Failure Actions**:
- Check panel positioning
- Verify key handlers don't interfere
- Adjust panel sizes if needed

---

### TC7: Toggle Multiple Times (Stress Test)
**Objective**: Verify Event Inspector handles rapid toggling

**Steps**:
1. Rapidly press F9 ten times (on/off/on/off...)
2. Let inspector settle in "enabled" state
3. Observe panel
4. Move player
5. Toggle off (F9)

**Expected Results**:
- ✅ No crashes or exceptions
- ✅ Panel state matches expected (visible when enabled, hidden when disabled)
- ✅ Event metrics continue to collect correctly
- ✅ No memory leaks (check memory usage)
- ✅ Recent events cleared when re-enabled (due to ResetTimings())

**Pass Criteria**:
- No crashes after rapid toggling
- State consistency maintained
- Metrics continue to work

**Failure Actions**:
- Check toggle logic for race conditions
- Verify ResetTimings() is called appropriately
- Monitor memory usage over time

---

### TC8: Event Inspector with No Events
**Objective**: Verify graceful handling when no events are published

**Steps**:
1. Start game but don't move player
2. Enable Event Inspector (F9) immediately
3. Wait 10 seconds without any input
4. Observe panel

**Expected Results**:
- ✅ Panel displays "No events recorded" or empty list
- ✅ No null reference exceptions
- ✅ Panel remains stable
- ✅ Once player moves, events appear normally

**Pass Criteria**:
- No crashes with empty event list
- Panel remains functional

**Failure Actions**:
- Check null handling in adapter
- Verify UI handles empty collections

---

### TC9: Long-Running Session (Memory Leak Test)
**Objective**: Verify no memory leaks during extended use

**Steps**:
1. Enable Event Inspector (F9)
2. Play game normally for 10-15 minutes
3. Move player frequently
4. Monitor memory usage (Task Manager / Activity Monitor)
5. Toggle inspector off/on a few times during session

**Expected Results**:
- ✅ Memory usage remains stable (no continuous growth)
- ✅ No frame rate degradation over time
- ✅ Event log doesn't grow unbounded (max 100 entries)
- ✅ Performance remains consistent

**Pass Criteria**:
- Memory increase < 100 MB over 15 minutes
- FPS remains stable
- No memory leaks detected

**Failure Actions**:
- Check maxLogEntries configuration (should be 100)
- Verify old events are removed
- Check for event handler leaks

---

### TC10: Scene Transition (Disposal Test)
**Objective**: Verify proper cleanup when changing scenes

**Steps**:
1. Enable Event Inspector (F9)
2. Exit gameplay scene (if applicable in your game)
3. Re-enter gameplay scene
4. Enable Event Inspector again

**Expected Results**:
- ✅ No errors when exiting scene
- ✅ Event Inspector disposes cleanly
- ✅ No "object disposed" exceptions on re-entry
- ✅ Event Inspector works normally after re-entering scene

**Pass Criteria**:
- Clean disposal
- No exceptions
- Inspector works after scene reload

**Failure Actions**:
- Check Dispose() implementation in GameplayScene
- Verify panel is properly disposed
- Check for hanging event subscriptions

---

## Quick Smoke Test (5 Minutes)

If time is limited, run this abbreviated test:

1. ✅ Build project successfully
2. ✅ Run game
3. ✅ Press F9 → Panel appears
4. ✅ Move player → Events appear in panel
5. ✅ Press F9 → Panel disappears
6. ✅ Press F3 → Performance Overlay appears
7. ✅ Press F9 → Both panels visible
8. ✅ Game runs smoothly with both enabled
9. ✅ Exit game → No crashes

**Pass**: All 9 steps succeed
**Fail**: Any step fails or crashes occur

---

## Known Limitations (Expected Behavior)

1. **Event Inspector Disabled by Default**
   - Inspector starts hidden and disabled
   - This is intentional to minimize overhead
   - Use F9 to enable

2. **Refresh Rate**
   - Panel updates every 2 frames (~30 FPS at 60 FPS base)
   - This balances responsiveness vs. performance
   - Not every frame is captured

3. **Max Log Entries**
   - Recent events list limited to 100 entries
   - Oldest events are removed as new ones arrive
   - This prevents unbounded memory growth

4. **Metrics Cleared on Enable**
   - When toggling inspector on, previous metrics are cleared
   - This gives clean data for current session
   - Intentional design to avoid stale data

---

## Bug Reporting Template

If you find a bug during testing, report it with this format:

```
**Test Case**: TC# - Test Name
**Severity**: Critical / High / Medium / Low
**Description**: Brief description of the issue

**Steps to Reproduce**:
1. Step 1
2. Step 2
3. Step 3

**Expected Result**: What should happen
**Actual Result**: What actually happened

**Console Output / Errors**:
```
[Paste error messages here]
```

**Screenshots**: (if applicable)

**System Info**:
- OS: [macOS / Windows / Linux]
- .NET Version: [9.0]
- Build Configuration: [Debug / Release]

**Additional Context**: Any other relevant information
```

---

## Test Sign-Off

**Tester Name**: _________________
**Date Tested**: _________________
**Test Environment**: _________________

### Test Results Summary

| Test Case | Status | Notes |
|-----------|--------|-------|
| TC1: Enable Event Inspector | ⬜ Pass / ⬜ Fail | |
| TC2: View Events (Static) | ⬜ Pass / ⬜ Fail | |
| TC3: View Events (Movement) | ⬜ Pass / ⬜ Fail | |
| TC4: Disable Event Inspector | ⬜ Pass / ⬜ Fail | |
| TC5: Performance Overhead | ⬜ Pass / ⬜ Fail | |
| TC6: Simultaneous Panels | ⬜ Pass / ⬜ Fail | |
| TC7: Rapid Toggling | ⬜ Pass / ⬜ Fail | |
| TC8: No Events Handling | ⬜ Pass / ⬜ Fail | |
| TC9: Memory Leak Test | ⬜ Pass / ⬜ Fail | |
| TC10: Scene Transition | ⬜ Pass / ⬜ Fail | |

**Overall Status**: ⬜ PASS / ⬜ FAIL

**Critical Issues Found**: _________________

**Recommendation**: ⬜ Ready for Production / ⬜ Needs Fixes / ⬜ Requires Further Testing

---

**Document Version**: 1.0
**Last Updated**: December 3, 2025
**Maintained By**: QA Team
