# Hot-Reload Testing Guide for Event-Driven CSX Scripts

## Overview
This guide provides step-by-step instructions for testing hot-reload functionality with event-driven CSX scripts in MonoBall Framework.

## Prerequisites
- MonoBall Framework.Game running in development mode
- CSX scripting service enabled
- Test map with ice tiles and tall grass configured

---

## Test 1: Ice Tile Hot-Reload

### Setup
1. Configure a test map with ice tiles at coordinates (10, 5) to (15, 5)
2. Assign `ice_tile.csx` to those tiles in map data
3. Start the game and load the test map

### Test Steps

#### Step 1: Initial Behavior
1. Move player to position (9, 5)
2. Move right onto ice tile (10, 5)
3. **Expected**: Player slides continuously right until hitting obstacle or non-ice tile
4. **Verify**: Sound effect plays ("ice_slide")
5. **Verify**: Movement speed increases to 2.0f during slide

#### Step 2: Modify Script (First Hot-Reload)
1. While game is running, edit `ice_tile.csx`
2. Change line 43: `movement.Speed = 2.0f;` to `movement.Speed = 3.0f;`
3. Change line 27: `ctx.Effects.PlaySound("ice_slide");` to `ctx.Effects.PlaySound("ice_slide_fast");`
4. Save the file

#### Step 3: Verify Hot-Reload
1. Move player off ice tiles
2. Move back onto ice tile (10, 5)
3. **Expected**: Player slides FASTER (3.0f speed)
4. **Expected**: New sound effect plays ("ice_slide_fast")
5. **Verify**: No game restart was required

#### Step 4: Test Event Handler Persistence
1. Edit `ice_tile.csx` again
2. Add debug logging to `OnMovementCompleted`:
```csharp
OnMovementCompleted(evt => {
    Console.WriteLine($"[HOT-RELOAD TEST] Movement completed at {evt.NewPosition}");
    // ... rest of handler
});
```
3. Save file
4. Step on ice tile multiple times
5. **Expected**: Console shows debug messages
6. **Verify**: No duplicate event handlers (only one message per movement)

#### Step 5: Test Event Handler Cleanup
1. Check game memory using debug tools
2. Note event handler count
3. Perform 5 hot-reloads (edit and save ice_tile.csx 5 times)
4. Check event handler count again
5. **Expected**: Handler count should NOT increase (old handlers cleaned up)
6. **Verify**: No memory leaks

---

## Test 2: Tall Grass Hot-Reload

### Setup
1. Configure tall grass tiles at coordinates (5, 10) to (10, 10)
2. Assign `tall_grass.csx` to those tiles
3. Start game at position (5, 9)

### Test Steps

#### Step 1: Initial Behavior
1. Step onto tall grass tile (5, 10)
2. **Expected**: Grass rustle animation plays
3. **Expected**: 10% chance of wild encounter
4. Walk back and forth 20 times (should trigger ~2 encounters)

#### Step 2: Modify Encounter Rate (Hot-Reload)
1. Edit `tall_grass.csx` while game running
2. Change line 9: `public float encounterRate = 0.10f;` to `public float encounterRate = 1.0f;`
3. Save file

#### Step 3: Verify Hot-Reload
1. Step on tall grass tile
2. **Expected**: Wild encounter triggers EVERY time (100% rate)
3. Repeat 5 times
4. **Verify**: All 5 steps trigger encounters

#### Step 4: Modify Pokemon Pool
1. Edit `tall_grass.csx`
2. Change line 10: `public string[] wildPokemon = new[] { "Pidgey", "Rattata", "Caterpie" };`
   to: `public string[] wildPokemon = new[] { "Mew", "Mewtwo", "Arceus" };`
3. Save file

#### Step 5: Verify Pokemon Pool Hot-Reload
1. Step on grass (triggers encounter at 100% rate)
2. **Expected**: Wild Pokemon is one of: Mew, Mewtwo, or Arceus
3. Repeat 3 times
4. **Verify**: Never see Pidgey/Rattata/Caterpie

#### Step 6: Stress Test Event Handlers
1. Edit and save `tall_grass.csx` 10 times rapidly
2. Step on grass tile after each edit
3. **Expected**: Each edit takes effect immediately
4. **Expected**: No crashes or null reference errors
5. **Verify**: Performance remains stable

---

## Test 3: Cross-Script Hot-Reload

### Setup
Use both ice tiles and tall grass in same map

### Test Steps

#### Step 1: Baseline Behavior
1. Step on ice tile → slides
2. Step on grass tile → wild encounter
3. **Verify**: Both behaviors work independently

#### Step 2: Hot-Reload Ice While on Grass
1. Stand on grass tile (no movement)
2. Edit `ice_tile.csx` (change sliding speed)
3. Save file
4. Move off grass onto ice
5. **Expected**: Ice tile uses NEW sliding speed
6. **Verify**: Grass behavior unchanged

#### Step 3: Hot-Reload Grass While on Ice
1. Stand on ice tile (sliding)
2. Edit `tall_grass.csx` (change encounter rate)
3. Save file
4. Complete ice slide, move onto grass
5. **Expected**: Grass uses NEW encounter rate
6. **Verify**: Ice behavior unchanged

---

## Test 4: Performance Testing

### Metrics to Track

#### Before Hot-Reload
1. Record frame time (ms)
2. Record event handler count
3. Record memory usage (MB)

#### During Hot-Reload
1. Trigger hot-reload (edit and save script)
2. Measure reload time (should be < 100ms)
3. Check for frame drops

#### After Hot-Reload
1. Record frame time (ms)
2. Record event handler count (should match "before")
3. Record memory usage (should not increase significantly)

### Success Criteria
- ✅ Hot-reload completes in < 100ms
- ✅ No frame drops during reload
- ✅ Memory usage increases < 1MB per reload
- ✅ Frame time remains stable (< 0.1ms increase)
- ✅ Event handlers don't accumulate

---

## Test 5: Error Handling During Hot-Reload

### Test Syntax Errors

#### Step 1: Introduce Syntax Error
1. Edit `ice_tile.csx`
2. Remove closing brace from `ContinueSliding` method
3. Save file

#### Expected Behavior
- ❌ Script fails to compile
- ✅ Old version continues running
- ✅ Error logged to console with line number
- ✅ Game remains stable

#### Step 2: Fix Syntax Error
1. Restore closing brace
2. Save file
3. **Expected**: Script compiles successfully
4. **Expected**: New version loads and runs

### Test Runtime Errors

#### Step 1: Introduce Null Reference
1. Edit `ice_tile.csx`
2. In `ContinueSliding`, change:
   ```csharp
   var movement = entity.Get<MovementComponent>();
   ```
   to:
   ```csharp
   var movement = null; // Force null reference
   ```
3. Save file

#### Step 2: Trigger Runtime Error
1. Step on ice tile
2. **Expected**: Exception logged to console
3. **Expected**: Game continues running (doesn't crash)
4. **Expected**: Script marked as failed

---

## Test 6: Event Handler Memory Management

### Test for Memory Leaks

#### Setup Tools
```csharp
// Add to ice_tile.csx for testing
private static int instanceCount = 0;
public IceTile() {
    instanceCount++;
    Console.WriteLine($"IceTile instance created. Total: {instanceCount}");
}

~IceTile() {
    instanceCount--;
    Console.WriteLine($"IceTile instance destroyed. Total: {instanceCount}");
}
```

#### Test Steps
1. Load script (instance count = 1)
2. Hot-reload 5 times
3. Wait for garbage collection
4. **Expected**: Instance count returns to 1 (old instances destroyed)
5. **Verify**: No memory leaks

---

## Automated Test Script

Create `hot_reload_test.csx` for automated testing:

```csharp
// hot_reload_test.csx - Automated hot-reload validation
using System.Diagnostics;

public class HotReloadTest : TypeScriptBase {
    public override void Execute(ScriptContext ctx) {
        var watch = Stopwatch.StartNew();

        // Test 1: Reload ice_tile.csx
        var result1 = ctx.Scripting.ReloadScript("ice_tile.csx");
        ctx.Log.Info($"Ice tile reload: {result1.Success} ({watch.ElapsedMilliseconds}ms)");

        // Test 2: Reload tall_grass.csx
        watch.Restart();
        var result2 = ctx.Scripting.ReloadScript("tall_grass.csx");
        ctx.Log.Info($"Tall grass reload: {result2.Success} ({watch.ElapsedMilliseconds}ms)");

        // Test 3: Get event handler count
        var handlerCount = ctx.EventSystem.GetHandlerCount();
        ctx.Log.Info($"Active event handlers: {handlerCount}");

        // Test 4: Memory usage
        var memoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
        ctx.Log.Info($"Memory usage: {memoryMB:F2} MB");

        // Report
        if (result1.Success && result2.Success && watch.ElapsedMilliseconds < 100) {
            ctx.Log.Info("✅ Hot-reload test PASSED");
        } else {
            ctx.Log.Error("❌ Hot-reload test FAILED");
        }
    }
}

return new HotReloadTest();
```

---

## Success Criteria Summary

### Functional Requirements
- ✅ Scripts reload without game restart
- ✅ Changes take effect immediately
- ✅ Event handlers register correctly after reload
- ✅ Old event handlers are cleaned up

### Performance Requirements
- ✅ Reload time < 100ms
- ✅ No frame drops during reload
- ✅ Memory overhead < 1MB per reload
- ✅ Event overhead < 0.1ms per event

### Reliability Requirements
- ✅ Syntax errors don't crash game
- ✅ Runtime errors are logged gracefully
- ✅ Multiple reloads don't accumulate handlers
- ✅ No memory leaks after 100 reloads

---

## Troubleshooting

### Issue: Hot-Reload Not Working
**Symptoms**: Changes don't take effect after saving
**Solutions**:
1. Check file watcher is enabled in config
2. Verify script file path is correct
3. Check console for compilation errors
4. Ensure development mode is enabled

### Issue: Duplicate Event Handlers
**Symptoms**: Events fire multiple times
**Solutions**:
1. Check event handler cleanup in ScriptService
2. Verify old handlers are unsubscribed during reload
3. Add logging to track handler registration/unregistration

### Issue: Performance Degradation
**Symptoms**: Frame time increases after reloads
**Solutions**:
1. Profile event handler count (should not grow)
2. Check for object pooling in event system
3. Monitor garbage collection frequency
4. Review script for expensive allocations

---

## Related Documentation
- `/docs/scripting/unified-scripting-interface.md` - Event handler registration
- `/docs/scripting/csx-scripting-analysis.md` - Hot-reload architecture
- `/examples/csx-event-driven/README.md` - Example script documentation
