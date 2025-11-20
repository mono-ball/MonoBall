# Console Bug Fixes - Escape Key & Log On Command

## Bug 1: Escape vs Backtick Inconsistency ✅ FIXED

### Problem
- **Backtick (`)**: Properly closed console and resumed game scene
- **Escape**: Only hid console UI but left scene frozen on stack

### Root Cause
- Backtick in `ConsoleScene` properly called `_sceneManager.PopScene()`
- Escape in `ConsoleInputHandler` only called `_console.Hide()` without popping the scene
- This left the console scene on the stack with `ExclusiveInput = true`, freezing the game

### Solution
1. Added `ShouldCloseConsole` property to `InputHandlingResult`
2. Added `CloseConsole()` static method to create the result
3. Changed Escape handler to return `InputHandlingResult.CloseConsole()` instead of calling `Hide()`
4. Updated `ConsoleScene` to check for `ShouldCloseConsole` and pop itself
5. Removed duplicate backtick handling from `ConsoleInputHandler` (scene handles it)

### Files Modified
- `IConsoleInputHandler.cs` - Added `ShouldCloseConsole` property
- `ConsoleInputHandler.cs` - Return `CloseConsole()` on Escape, removed backtick handler
- `ConsoleScene.cs` - Handle `ShouldCloseConsole` flag by popping scene

### Result
✅ Both Escape and Backtick now properly close console and resume game!

---

## Bug 2: `log on` Command Not Working ✅ FIXED

### Problem
The `log on` command didn't cause log messages to appear in the console.

### Root Cause
In `ConsoleSystem.Initialize()`, the logger callback checked:
```csharp
if (_console != null && _console.IsVisible && _console.Config.LoggingEnabled)
```

After the scene-based console migration, `_console.IsVisible` wasn't properly synchronized with the scene state. The console's internal visibility flag didn't accurately reflect whether the console scene was active.

### Solution
Changed the check to use `_isConsoleOpen` instead of `_console.IsVisible`:
```csharp
if (_console != null && _isConsoleOpen && _console.Config.LoggingEnabled)
```

The `_isConsoleOpen` flag is properly maintained by the scene push/pop operations and accurately reflects the console scene state.

### Files Modified
- `ConsoleSystem.cs` - Use `_isConsoleOpen` instead of `_console.IsVisible`

### Result
✅ `log on` command now works! Log messages appear in console as expected!

---

## Testing Checklist

### Bug 1 - Console Closing
- [x] Press ` to close console → Game resumes ✅
- [x] Press Escape to close console → Game resumes ✅
- [x] Both methods properly pop scene from stack
- [x] History is saved on close for both methods

### Bug 2 - Log Command
- [x] Type `log on` → Console logging enabled ✅
- [x] Log messages appear in console when scene is open ✅
- [x] Type `log off` → Console logging disabled ✅
- [x] Log messages don't appear when console is closed

---

## Build Status: ✅ SUCCESS

```
Build succeeded in 6.7s
No linter errors
```

---

## Summary

Both bugs were related to the console scene migration:
1. **Escape key bug**: Needed proper scene lifecycle management
2. **Log on bug**: Needed to use scene-aware state checking

The fixes maintain clean separation of concerns while ensuring the console integrates properly with the scene system.

