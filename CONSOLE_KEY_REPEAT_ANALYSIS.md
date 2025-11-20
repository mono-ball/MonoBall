# Console Key Repeat Analysis

## Current State

### ✅ Keys WITH Key Repeat (Working Correctly)

These keys already support key repeat through the `HandleTextInput` and specialized handlers:

1. **Character Input** (Letters, numbers, symbols) - ✅ Has repeat
2. **Backspace** - ✅ Has repeat (via HandleTextInput)
3. **Delete** - ✅ Has repeat (via HandleTextInput)
4. **Left Arrow** (normal) - ✅ Has repeat (via HandleTextInput)
5. **Right Arrow** (normal) - ✅ Has repeat (via HandleTextInput)
6. **Up Arrow** (in multi-line or autocomplete) - ✅ Has repeat (via HandleSuggestionNavigation)
7. **Down Arrow** (in multi-line or autocomplete) - ✅ Has repeat (via HandleSuggestionNavigation)
8. **Home** - ✅ Has repeat (via HandleTextInput)
9. **End** - ✅ Has repeat (via HandleTextInput)
10. **Left/Right in autocomplete** - ✅ Has repeat (via HandleSuggestionScrolling)

### ✅ ALL KEYS NOW HAVE REPEAT - IMPLEMENTATION COMPLETE!

Previously missing keys that now have repeat support:

#### High Priority (Essential for smooth UX) - ✅ DONE

1. **PageUp / PageDown**
   - Current: `WasKeyJustPressed` - NO repeat
   - Should: Repeat for smooth scrolling through output/suggestions
   - Lines: 441, 457

2. **Ctrl+Left / Ctrl+Right** (Word navigation)
   - Current: `WasKeyJustPressed` - NO repeat
   - Should: Repeat for fast word-by-word navigation
   - Lines: 489, 496

3. **Ctrl+Backspace / Ctrl+Delete** (Delete word)
   - Current: `WasKeyJustPressed` - NO repeat
   - Should: Repeat for fast multi-word deletion
   - Lines: 503, 521

4. **F3 / Shift+F3** (Navigate search results)
   - Current: `WasKeyJustPressed` - NO repeat
   - Should: Repeat for fast navigation through many matches
   - Line: 1502

#### Medium Priority (Nice to have) - ✅ DONE

5. **Ctrl+Plus / Ctrl+Minus** (Font size)
   - Current: `WasKeyJustPressed` - NO repeat
   - Should: Repeat for smooth font size adjustment
   - Lines: 1447, 1458

6. **Ctrl+R / Ctrl+S** (Reverse-i-search navigation)
   - Current: `WasKeyJustPressed` - NO repeat
   - Should: Repeat for fast cycling through matches
   - Lines: 340, 350

7. **Ctrl+Shift+Up / Ctrl+Shift+Down** (Parameter hint overloads)
   - ✅ IMPLEMENTED: `HandleParameterHintNavigation()`
   - Now repeats for fast cycling through overloads

8. **Ctrl+Z / Ctrl+Y** (Undo/Redo)
   - ✅ IMPLEMENTED: `HandleUndoRedo()`
   - Now repeats for rapid multi-level undo/redo

### ✅ Keys Correctly WITHOUT Repeat

These keys should NOT repeat (single-fire actions):

1. **Backtick (`)** - Toggle console
2. **Enter** - Execute command / Accept suggestion
3. **Tab** - Accept autocomplete
4. **Escape** - Close popups/console
5. **Ctrl+A** - Select all
6. **Ctrl+C** - Copy
7. **Ctrl+X** - Cut
8. **Ctrl+V** - Paste
9. **Ctrl+F** - Open search
10. **Ctrl+R** - Start reverse-i-search
11. **Ctrl+Space** - Trigger autocomplete
12. **Alt+[** - Collapse all sections
13. **Alt+]** - Expand all sections
14. **F1** - Show/hide documentation
15. **F2-F12** (Bookmark toggle/execute) - Should NOT repeat
16. **Ctrl+Home** - Jump to top (instant jump)
17. **Ctrl+End** - Jump to bottom (instant jump)
18. **Ctrl+0** - Reset font size (instant reset)

## Implementation Strategy

### Pattern to Follow

The existing `HandleSuggestionNavigation` and `HandleSuggestionScrolling` methods provide the pattern:

```csharp
private InputHandlingResult HandleKeyWithRepeat(KeyboardState keyboardState, KeyboardState previousKeyboardState, float deltaTime)
{
    Keys? currentKey = null;
    
    // Determine which key is pressed
    if (keyboardState.IsKeyDown(Keys.SomeKey))
        currentKey = Keys.SomeKey;
    
    if (currentKey.HasValue)
    {
        bool shouldProcess = false;
        
        // Check if key was just pressed
        if (!previousKeyboardState.IsKeyDown(currentKey.Value))
        {
            _lastHeldKey = currentKey.Value;
            _keyHoldTime = 0;
            _lastKeyRepeatTime = 0;
            shouldProcess = true;
        }
        // Check for key repeat
        else if (_lastHeldKey == currentKey.Value)
        {
            _keyHoldTime += deltaTime;
            
            if (_keyHoldTime >= InitialKeyRepeatDelay)
            {
                _lastKeyRepeatTime += deltaTime;
                
                if (_lastKeyRepeatTime >= KeyRepeatInterval)
                {
                    shouldProcess = true;
                    _lastKeyRepeatTime = 0;
                }
            }
        }
        
        if (shouldProcess)
        {
            // Perform the action
        }
        
        return InputHandlingResult.Consumed;
    }
    
    return InputHandlingResult.None;
}
```

### Required State Variables

We may need additional state variables for tracking different key repeat contexts:
- `_lastHeldPageKey` - for PageUp/PageDown
- `_lastHeldWordNavKey` - for Ctrl+Left/Right
- `_lastHeldDeleteWordKey` - for Ctrl+Backspace/Delete
- `_lastHeldSearchNavKey` - for F3/Shift+F3
- `_lastHeldFontSizeKey` - for Ctrl+Plus/Minus
- etc.

OR we could create a more generic key repeat tracker that handles multiple keys.

## ✅ Implementation Complete!

All 9 handlers have been implemented:

1. ✅ **PageUp/PageDown** - `HandlePageScrolling()`
2. ✅ **Ctrl+Left/Right** - `HandleWordNavigation()`
3. ✅ **Ctrl+Backspace/Delete** - `HandleWordDeletion()`
4. ✅ **F3/Shift+F3** - Updated `HandleFunctionKeys()`
5. ✅ **Ctrl+Plus/Minus** - Updated `HandleFontSizeChanges()`
6. ✅ **Ctrl+R/S in reverse-i-search** - `HandleReverseSearchNavigation()`
7. ✅ **Ctrl+Shift+Up/Down for parameter hints** - `HandleParameterHintNavigation()`
8. ✅ **Ctrl+Z/Y for undo/redo** - `HandleUndoRedo()`

## Testing Checklist

After implementing key repeat for each key:
- [ ] Key fires once immediately when pressed
- [ ] Key waits InitialKeyRepeatDelay before repeating
- [ ] Key repeats at KeyRepeatInterval while held
- [ ] Key stops repeating when released
- [ ] Switching to different key cancels previous repeat
- [ ] Works correctly with modifier keys (Ctrl, Shift, Alt)
- [ ] Doesn't interfere with other key handling

## Notes

- `InitialKeyRepeatDelay` = 0.5 seconds
- `KeyRepeatInterval` = 0.05 seconds (20 repeats per second)
- These values provide good feel for keyboard navigation

