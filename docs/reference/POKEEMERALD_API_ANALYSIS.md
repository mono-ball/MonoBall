# Pokeemerald Script Functions vs Current API Analysis

This document compares common pokeemerald script commands with the current API implementations to identify missing functionality.

## Current API Services

1. **PlayerApiService** - Player management, money, movement
2. **MapApiService** - Map queries, transitions, walkability
3. **NpcApiService** - NPC movement, facing, paths
4. **GameStateApiService** - Flags, variables, random
5. **DialogueApiService** - Message display
6. **EffectApiService** - Visual effects

---

## Missing Functions by Category

### 1. Item Management (Missing - New API Needed)

**Pokeemerald Commands:**
- `giveitem [item], [quantity]` - Give item to player
- `additem [item], [quantity]` - Add item to bag
- `removeitem [item], [quantity]` - Remove item from bag
- `checkitem [item], [quantity]` - Check if player has item
- `checkitemspace [item], [quantity]` - Check if bag has space
- `checkitemtype [item]` - Get item type (key item, TM, etc.)
- `itemquantity [item]` - Get quantity of item in bag

**Current Status:** ❌ Not implemented

**Recommendation:** Create `IItemApi` with:
- `GiveItem(string itemId, int quantity)`
- `RemoveItem(string itemId, int quantity)`
- `HasItem(string itemId, int quantity = 1)`
- `GetItemQuantity(string itemId)`
- `CheckItemSpace(string itemId, int quantity)`

---

### 2. Player Movement Enhancements

**Pokeemerald Commands:**
- `walk [direction], [steps]` - Move player N steps
- `walkplayer [x], [y]` - Walk player to coordinates
- `applymovement [object], [movement_script]` - Apply movement script
- `waitmovement [object]` - Wait for movement to complete
- `lockall` / `releaseall` - Lock/unlock all NPCs
- `lock` / `release` - Lock/unlock specific NPC

**Current Status:** ⚠️ Partially implemented
- ✅ `SetPlayerFacing()` - Change facing
- ✅ `SetPlayerMovementLocked()` - Lock/unlock movement
- ❌ `WalkPlayer(int x, int y)` - Walk to coordinates
- ❌ `WalkPlayer(Direction direction, int steps)` - Walk N steps
- ❌ `WaitForPlayerMovement()` - Wait for movement completion

**Recommendation:** Add to `IPlayerApi`:
- `WalkTo(int x, int y)` - Walk player to coordinates
- `Walk(Direction direction, int steps)` - Walk N steps in direction
- `WaitForMovement()` - Wait for current movement to complete

---

### 3. NPC Movement Enhancements

**Pokeemerald Commands:**
- `faceplayer` - Make NPC face player
- `faceobject [object], [direction]` - Face specific object
- `applymovement [object], [movement_script]` - Apply movement script
- `waitmovement [object]` - Wait for movement
- `removeobject [object]` - Remove NPC from map
- `addobject [object]` - Add NPC to map
- `showobject [object]` / `hideobject [object]` - Show/hide NPC

**Current Status:** ⚠️ Partially implemented
- ✅ `MoveNPC()` - Move in direction
- ✅ `FaceDirection()` - Face direction
- ✅ `FaceEntity()` - Face another entity
- ✅ `SetNPCPath()` - Set patrol path
- ❌ `FacePlayer(Entity npc)` - Face player (convenience method)
- ❌ `WaitForNPCMovement(Entity npc)` - Wait for movement
- ❌ `RemoveNPC(Entity npc)` - Remove from map
- ❌ `ShowNPC(Entity npc)` / `HideNPC(Entity npc)` - Visibility control

**Recommendation:** Add to `INPCApi`:
- `FacePlayer(Entity npc)` - Convenience method to face player
- `WaitForMovement(Entity npc)` - Wait for NPC movement
- `RemoveNPC(Entity npc)` - Remove NPC from world
- `SetNPCVisible(Entity npc, bool visible)` - Show/hide NPC

---

### 4. Map/Warp Functions

**Pokeemerald Commands:**
- `warp [map], [bank], [x], [y]` - Warp player
- `warpmuted [map], [bank], [x], [y]` - Warp without sound
- `setwarp [warp_id], [map], [bank], [x], [y]` - Set warp point
- `getplayerxy [var_x], [var_y]` - Get player position to variables
- `comparexy [x], [y]` - Compare player position

**Current Status:** ⚠️ Partially implemented
- ✅ `TransitionToMap()` - Warp to map
- ✅ `GetCurrentMapId()` - Get current map
- ✅ `GetPlayerPosition()` - Get player position
- ❌ `TransitionToMapMuted()` - Warp without sound/effects
- ❌ `SetWarpPoint()` - Define warp point
- ❌ `GetWarpPoint()` - Get warp point data

**Recommendation:** Add to `IMapApi`:
- `TransitionToMapMuted(MapRuntimeId mapId, int x, int y)` - Silent warp
- `SetWarpPoint(string warpId, MapRuntimeId mapId, int x, int y)` - Set warp
- `GetWarpPoint(string warpId)` - Get warp point data

---

### 5. Dialogue/Message Enhancements

**Pokeemerald Commands:**
- `msgbox [text], [type]` - Show message box (normal, yes/no, etc.)
- `message [text]` - Show message
- `closemsg` - Close message
- `waitmessage` - Wait for message to close
- `showmsgbox` - Show message box UI
- `hidemsgbox` - Hide message box

**Current Status:** ⚠️ Partially implemented
- ✅ `ShowMessage()` - Display message
- ✅ `ClearMessages()` - Clear messages
- ✅ `IsDialogueActive` - Check if active
- ❌ `ShowMessageBox(string message, MessageBoxType type)` - Message box types
- ❌ `WaitForMessage()` - Wait for message to close
- ❌ `ShowYesNoPrompt(string message)` - Yes/No dialog

**Recommendation:** Add to `IDialogueApi`:
- `ShowMessageBox(string message, MessageBoxType type)` - Support different types
- `ShowYesNoPrompt(string message)` - Returns bool
- `WaitForMessage()` - Wait for message to close
- `CloseMessage()` - Close current message

---

### 6. Game State / Variables Enhancements

**Pokeemerald Commands:**
- `setvar [var], [value]` - Set variable (numeric)
- `addvar [var], [value]` - Add to variable
- `subvar [var], [value]` - Subtract from variable
- `copyvar [dest], [src]` - Copy variable
- `compare [var], [value]` - Compare variable
- `vary [var], [min], [max]` - Randomize variable

**Current Status:** ⚠️ Partially implemented
- ✅ `SetVariable()` - String variables only
- ✅ `GetVariable()` - String variables only
- ✅ `SetFlag()` / `GetFlag()` - Boolean flags
- ❌ Numeric variables (int/float)
- ❌ Variable arithmetic (add, subtract)
- ❌ Variable comparison
- ❌ Copy variable

**Recommendation:** Add to `IGameStateApi`:
- `SetNumericVariable(string key, int value)`
- `GetNumericVariable(string key, int defaultValue = 0)`
- `AddToVariable(string key, int amount)`
- `SubtractFromVariable(string key, int amount)`
- `CompareVariable(string key, int value)` - Returns ComparisonResult
- `CopyVariable(string sourceKey, string destKey)`
- `RandomizeVariable(string key, int min, int max)`

---

### 7. Time Management (Missing - New API Needed)

**Pokeemerald Commands:**
- `gettime` - Get current time (hour, minute, second)
- `settime [hour], [minute]` - Set game time
- `checktime [time]` - Check if time matches (morning/day/night)

**Current Status:** ❌ Not implemented

**Recommendation:** Create `ITimeApi` with:
- `GetTime()` - Returns (hour, minute, second)
- `SetTime(int hour, int minute)`
- `GetTimeOfDay()` - Returns TimeOfDay enum (Morning/Day/Evening/Night)
- `IsTimeOfDay(TimeOfDay timeOfDay)`

---

### 8. Pokemon/Party Management (Missing - New API Needed)

**Pokeemerald Commands:**
- `givepokemon [species], [level], [item]` - Give Pokemon
- `addpokemon [species], [level]` - Add to party
- `removepokemon [species]` - Remove from party
- `checkpokemon [species]` - Check if has Pokemon
- `getpartysize` - Get party count
- `getpartymon [slot]` - Get party Pokemon data

**Current Status:** ❌ Not implemented

**Recommendation:** Create `IPokemonApi` with:
- `GivePokemon(string speciesId, int level, string? heldItem = null)`
- `AddPokemonToParty(string speciesId, int level)`
- `RemovePokemonFromParty(string speciesId)`
- `HasPokemon(string speciesId)`
- `GetPartySize()`
- `GetPartyPokemon(int slot)` - Returns Pokemon data

---

### 9. Sound/Music (Missing - New API Needed)

**Pokeemerald Commands:**
- `playsound [sound_id]` - Play sound effect
- `playmusic [music_id]` - Play background music
- `fademusic [fade_type]` - Fade music
- `stopmusic` - Stop music
- `playmoncry [species], [effect]` - Play Pokemon cry

**Current Status:** ❌ Not implemented

**Recommendation:** Create `IAudioApi` with:
- `PlaySound(string soundId)`
- `PlayMusic(string musicId)`
- `FadeMusic(FadeType fadeType)`
- `StopMusic()`
- `PlayPokemonCry(string speciesId)`

---

### 10. Screen Effects

**Pokeemerald Commands:**
- `fadescreen [fade_type]` - Fade screen (in/out/both)
- `fadescreenspeed [fade_type], [speed]` - Fade with speed
- `special [special_id]` - Various special effects

**Current Status:** ⚠️ Partially implemented
- ✅ `SpawnEffect()` - Visual effects
- ❌ `FadeScreen()` - Screen fade
- ❌ `FadeScreenSpeed()` - Fade with speed control

**Recommendation:** Add to `IEffectApi`:
- `FadeScreen(FadeType fadeType)` - Fade in/out/both
- `FadeScreenSpeed(FadeType fadeType, float speed)` - Fade with speed

---

### 11. Wait/Delay Functions

**Pokeemerald Commands:**
- `wait [frames]` - Wait N frames
- `delay [frames]` - Delay execution
- `waitmovement [object]` - Wait for movement
- `waitmessage` - Wait for message

**Current Status:** ⚠️ Partially implemented
- ❌ `Wait(int frames)` - Wait frames
- ❌ `WaitSeconds(float seconds)` - Wait seconds
- ❌ `WaitForMovement()` - Already covered above

**Recommendation:** Add to `IGameStateApi` or create `IWaitApi`:
- `Wait(int frames)` - Wait N frames
- `WaitSeconds(float seconds)` - Wait N seconds
- `WaitForMovement(Entity? entity = null)` - Wait for movement

---

### 12. Conditional/Control Flow

**Pokeemerald Commands:**
- `if [condition]` - Conditional execution
- `goto [label]` - Jump to label
- `call [script]` - Call script
- `return` - Return from script
- `compare [var], [value]` - Compare for conditionals

**Current Status:** ⚠️ Partially implemented
- ✅ Basic script execution
- ❌ Script labels/gotos (handled by script engine)
- ❌ Script calling (handled by script engine)
- ❌ Conditional helpers

**Recommendation:** These are typically handled by the script engine itself, but could add helpers:
- `CompareVariable()` - Already recommended above
- `CompareFlag()` - Check flag for conditionals

---

## Summary

### New APIs Needed:
1. **IItemApi** - Item/inventory management
2. **ITimeApi** - Time management
3. **IPokemonApi** - Pokemon/party management
4. **IAudioApi** - Sound/music management
5. **IWaitApi** (optional) - Wait/delay functions

### Enhancements to Existing APIs:

**IPlayerApi:**
- `WalkTo(int x, int y)`
- `Walk(Direction direction, int steps)`
- `WaitForMovement()`

**IMapApi:**
- `TransitionToMapMuted()`
- `SetWarpPoint()` / `GetWarpPoint()`

**INPCApi:**
- `FacePlayer(Entity npc)`
- `WaitForMovement(Entity npc)`
- `RemoveNPC(Entity npc)`
- `SetNPCVisible(Entity npc, bool visible)`

**IDialogueApi:**
- `ShowMessageBox(string message, MessageBoxType type)`
- `ShowYesNoPrompt(string message)` - Returns bool
- `WaitForMessage()`
- `CloseMessage()`

**IGameStateApi:**
- Numeric variable support (int/float)
- `AddToVariable()` / `SubtractFromVariable()`
- `CompareVariable()`
- `CopyVariable()`
- `RandomizeVariable()`

**IEffectApi:**
- `FadeScreen(FadeType fadeType)`
- `FadeScreenSpeed(FadeType fadeType, float speed)`

---

## Priority Recommendations

### High Priority (Core Gameplay):
1. **IItemApi** - Essential for item-based interactions
2. **IGameStateApi numeric variables** - Many scripts use numeric variables
3. **IDialogueApi message box types** - Yes/No prompts are common
4. **IPlayerApi WalkTo()** - Common movement pattern

### Medium Priority (Enhanced Features):
5. **ITimeApi** - Time-based events
6. **IAudioApi** - Sound/music for immersion
7. **IEffectApi FadeScreen()** - Common visual effect
8. **INPCApi FacePlayer()** - Common NPC behavior

### Low Priority (Nice to Have):
9. **IPokemonApi** - If Pokemon management is needed
10. **IWaitApi** - Can be handled by script engine
11. **Warp point management** - If warp system is complex





