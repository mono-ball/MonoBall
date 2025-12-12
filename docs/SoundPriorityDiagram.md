# Sound Priority System - Visual Reference

## Priority Hierarchy

```
┌─────────────────────────────────────────────────────────┐
│                    PRIORITY LEVELS                      │
│                 (Higher = More Protected)               │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  5 │ UI          │ ████████████████ │ NEVER EVICTED   │
│    │             │ Menu Confirm     │                  │
│    │             │ Error Beeps      │                  │
│    │             │ Notifications    │                  │
│    │                                                    │
│  4 │ Critical    │ ███████████████  │ NEVER EVICTED   │
│    │             │ Battle Moves     │                  │
│    │             │ Pokemon Cries    │                  │
│    │             │ Damage Effects   │                  │
│    │                                                    │
│  3 │ High        │ ████████████     │ Can Evict 0-2   │
│    │             │ Item Pickup      │                  │
│    │             │ Menu Navigation  │                  │
│    │             │ Capture Success  │                  │
│    │                                                    │
│  2 │ Normal      │ ████████         │ Can Evict 0-1   │
│    │ (DEFAULT)   │ Door Opening     │                  │
│    │             │ Item Use         │                  │
│    │             │ General Events   │                  │
│    │                                                    │
│  1 │ Low         │ ████             │ Can Evict 0     │
│    │             │ Footsteps        │                  │
│    │             │ Grass Rustle     │                  │
│    │             │ Minor Feedback   │                  │
│    │                                                    │
│  0 │ Background  │ ██               │ Evicted First   │
│    │             │ Wind Ambience    │                  │
│    │             │ Water Sounds     │                  │
│    │             │ Distant Birds    │                  │
└─────────────────────────────────────────────────────────┘
```

## Eviction Flow

```
New Sound Request Arrives
         │
         ▼
    ┌─────────┐
    │ Count   │ Yes    ┌──────────────┐
    │ < 32?   ├───────►│ Play Sound   │──► SUCCESS
    └────┬────┘        └──────────────┘
         │ No
         ▼
┌────────────────────┐
│ Find Eviction      │
│ Candidate          │
│                    │
│ Rules:             │
│ 1. Priority < New  │
│ 2. Priority < 4    │
│ 3. Oldest if tie   │
└────────┬───────────┘
         │
         ▼
    ┌─────────┐
    │ Found?  │ Yes    ┌──────────────┐
    │         ├───────►│ Evict & Play │──► SUCCESS
    └────┬────┘        └──────────────┘
         │ No
         ▼
    ┌──────────────┐
    │ Reject Sound │──► FAILURE
    └──────────────┘
```

## Example Scenarios

### Scenario 1: Normal Capacity Operation

```
Active Sounds: 15/32
New Sound: Priority Normal

┌──────────────────────────────────────┐
│  Sound Slots (32 total)              │
├──────────────────────────────────────┤
│  [1-15] ████████████████ (occupied)  │
│  [16-32] ░░░░░░░░░░░░░░░░ (free)     │
└──────────────────────────────────────┘

Result: ✓ Play in slot 16 (no eviction needed)
```

### Scenario 2: At Capacity - Priority Eviction

```
Active Sounds: 32/32
Distribution:
  - Background: 8
  - Low: 12
  - Normal: 8
  - High: 3
  - Critical: 1

New Sound: Priority High

┌──────────────────────────────────────────┐
│  Eviction Decision Tree                  │
├──────────────────────────────────────────┤
│  Check Background (0) < High (3) ✓       │
│    → Candidate: Oldest Background        │
│  Evict Oldest Background Sound           │
│  Play New High Priority Sound            │
└──────────────────────────────────────────┘

Result: ✓ Evict oldest Background, play new sound
```

### Scenario 3: Protected Sounds Block Eviction

```
Active Sounds: 32/32
Distribution:
  - Critical: 16
  - UI: 16

New Sound: Priority Normal

┌──────────────────────────────────────────┐
│  Eviction Decision Tree                  │
├──────────────────────────────────────────┤
│  Check Critical (4) < Normal (2) ✗       │
│  Check UI (5) < Normal (2) ✗             │
│  No evictable sounds found               │
└──────────────────────────────────────────┘

Result: ✗ Reject new sound (cannot evict Critical/UI)
```

### Scenario 4: Same Priority - Age Matters

```
Active Sounds: 32/32
All Priority: Normal

Sound Ages:
  [1] 5s old  [2] 4s old  [3] 3s old
  [4] 2s old  [5] 1s old  ... [32] 0.1s old

New Sound: Priority High

┌──────────────────────────────────────────┐
│  Eviction Decision Tree                  │
├──────────────────────────────────────────┤
│  Find: Priority < High (3) ✓             │
│    → All Normal (2) qualify              │
│  Find: Oldest among Normal               │
│    → Sound [1] (5 seconds old)           │
└──────────────────────────────────────────┘

Result: ✓ Evict Sound [1] (oldest), play new sound
```

## Priority Decision Matrix

```
┌──────────────┬─────────────────────────────────────────┐
│ Game System  │ Recommended Priority                    │
├──────────────┼─────────────────────────────────────────┤
│ Battle       │                                         │
│  - Moves     │ Critical (4) - Must not be interrupted  │
│  - Damage    │ Critical (4) - Important feedback       │
│  - Cries     │ Critical (4) - Character recognition    │
│              │                                         │
│ UI           │                                         │
│  - Confirm   │ UI (5) - Immediate responsive feedback  │
│  - Cancel    │ UI (5) - Immediate responsive feedback  │
│  - Error     │ UI (5) - Critical user notification     │
│  - Navigate  │ High (3) - Important but not critical   │
│              │                                         │
│ Items        │                                         │
│  - Key Item  │ High (3) - Important progression item   │
│  - Badge     │ Critical (4) - Major milestone          │
│  - TM/HM     │ High (3) - Significant items            │
│  - Regular   │ Normal (2) - Standard items             │
│              │                                         │
│ World        │                                         │
│  - Door      │ Normal (2) - Standard interaction       │
│  - Footsteps │ Low (1) - Continuous, less important    │
│  - Grass     │ Low (1) - Environmental feedback        │
│              │                                         │
│ Ambient      │                                         │
│  - Wind      │ Background (0) - Atmosphere only        │
│  - Water     │ Background (0) - Atmosphere only        │
│  - Birds     │ Background (0) - Atmosphere only        │
└──────────────┴─────────────────────────────────────────┘
```

## Performance Impact

```
┌────────────────────────────────────────────────────┐
│  Operation Performance                             │
├────────────────────────────────────────────────────┤
│                                                    │
│  Below Capacity (<32 sounds):                      │
│  ┌────────────────┐                               │
│  │ O(1)           │  No priority check             │
│  │ Instant play   │  Direct slot allocation        │
│  └────────────────┘                               │
│                                                    │
│  At Capacity (=32 sounds):                         │
│  ┌────────────────┐                               │
│  │ O(n) n=32      │  Scan active sounds            │
│  │ ~0.1ms         │  Find eviction candidate       │
│  └────────────────┘                               │
│                                                    │
│  Memory Overhead:                                  │
│  ┌────────────────┐                               │
│  │ 4 bytes/sound  │  Enum storage (int32)          │
│  │ 128 bytes max  │  32 sounds × 4 bytes           │
│  └────────────────┘                               │
└────────────────────────────────────────────────────┘
```

## Code Integration Points

```
Game Systems
     │
     ├─► BattleSystem ──► priority: Critical
     │        │
     │        └─► MoveSounds, Cries, Damage
     │
     ├─► UISystem ──────► priority: UI
     │        │
     │        └─► Confirm, Cancel, Error
     │
     ├─► ItemSystem ────► priority: High/Normal
     │        │
     │        └─► Based on item type
     │
     ├─► WorldSystem ───► priority: Normal/Low
     │        │
     │        └─► Doors, Footsteps, Grass
     │
     └─► AmbientSystem ─► priority: Background
              │
              └─► Wind, Water, Birds

           ▼
    NAudioSoundEffectManager
           │
           ├─► Priority-based eviction
           ├─► Age tracking
           └─► Protected sound enforcement
```

## Testing Checklist

```
Test Priority Hierarchy:
  ☐ Background evicted before Low
  ☐ Low evicted before Normal
  ☐ Normal evicted before High
  ☐ High evicted before Critical
  ☐ Critical never evicted
  ☐ UI never evicted

Test Age Ordering:
  ☐ Oldest sound evicted within same priority
  ☐ Newer sounds preserved within same priority

Test Capacity Limits:
  ☐ 32 sounds play successfully
  ☐ 33rd sound triggers eviction
  ☐ All Critical sounds block eviction
  ☐ Mixed priorities evict correctly

Test Edge Cases:
  ☐ All slots Critical - reject new sound
  ☐ All slots UI - reject new sound
  ☐ Single Background sound - evict successfully
  ☐ Equal priority + same timestamp - deterministic behavior
```
