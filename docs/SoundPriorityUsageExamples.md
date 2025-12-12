# Sound Priority System - Usage Examples

## Overview

The sound priority system provides intelligent eviction control when the maximum concurrent sounds limit (32) is reached. Sounds are prioritized to ensure critical gameplay feedback is never interrupted by less important sounds.

## Priority Levels

| Priority | Value | Description | Examples |
|----------|-------|-------------|----------|
| **Background** | 0 | Ambient sounds, lowest priority | Distant birds, wind, water ambience |
| **Low** | 1 | Environmental effects | Footsteps, rustling grass, minor feedback |
| **Normal** | 2 | Standard game sounds (default) | Door opening, item use, general interactions |
| **High** | 3 | Important gameplay feedback | Item pickup, menu navigation, capture success |
| **Critical** | 4 | Essential gameplay sounds | Battle moves, damage effects, Pokemon cries |
| **UI** | 5 | Interface sounds (highest) | Menu confirm/cancel, error beeps, notifications |

## Eviction Rules

1. **Lower priority sounds are evicted first** when capacity is reached
2. **Within the same priority**, the oldest sound is evicted
3. **Critical and UI sounds are never evicted** - new sounds are rejected instead
4. A new sound must have **higher priority** than existing sounds to evict them

## Code Examples

### Basic Usage

```csharp
// Play a standard sound (Normal priority by default)
soundManager.Play("se_door_open");

// Play with explicit priority
soundManager.Play("se_menu_select", volume: 1.0f, priority: SoundPriority.UI);
soundManager.Play("se_grass_rustle", volume: 0.5f, priority: SoundPriority.Low);
soundManager.Play("se_battle_hit", volume: 0.9f, priority: SoundPriority.Critical);
```

### Practical Scenarios

#### Battle System

```csharp
// Battle sounds should be Critical to never get interrupted
public void PlayBattleMove(string moveSound)
{
    _soundManager.Play(moveSound, volume: 1.0f, priority: SoundPriority.Critical);
}

// Pokemon cries during battle are also Critical
public void PlayPokemonCry(int pokemonId)
{
    var crySound = $"cry_{pokemonId}";
    _soundManager.Play(crySound, volume: 1.0f, priority: SoundPriority.Critical);
}
```

#### UI System

```csharp
// UI sounds should use UI priority to ensure responsive feedback
public void PlayMenuConfirm()
{
    _soundManager.Play("se_select", volume: 1.0f, priority: SoundPriority.UI);
}

public void PlayMenuCancel()
{
    _soundManager.Play("se_cancel", volume: 1.0f, priority: SoundPriority.UI);
}

public void PlayError()
{
    _soundManager.Play("se_error", volume: 1.0f, priority: SoundPriority.UI);
}
```

#### Ambient Sounds

```csharp
// Ambient sounds should use Background or Low priority
public void PlayAmbientWater()
{
    _soundManager.PlayLooping("se_water_ambient",
        volume: 0.3f,
        priority: SoundPriority.Background);
}

public void PlayFootstep()
{
    _soundManager.Play("se_footstep",
        volume: 0.7f,
        priority: SoundPriority.Low);
}
```

#### Item Pickup

```csharp
// Important feedback should use High priority
public void PlayItemPickup(ItemType itemType)
{
    var priority = itemType switch
    {
        ItemType.KeyItem => SoundPriority.High,
        ItemType.TM_HM => SoundPriority.High,
        ItemType.Badge => SoundPriority.Critical,  // Very important!
        _ => SoundPriority.Normal
    };

    _soundManager.Play("se_item_get", volume: 1.0f, priority: priority);
}
```

## Behavior at Max Capacity

### Example Scenario

Assume we have 32 sounds playing:
- 5 x Background priority
- 10 x Low priority
- 12 x Normal priority
- 3 x High priority
- 2 x Critical priority

#### Case 1: New Normal Sound
```csharp
// Will evict the oldest Background sound
soundManager.Play("se_door", priority: SoundPriority.Normal);
```

#### Case 2: New Critical Sound
```csharp
// Will evict the oldest Background sound (lowest priority available)
soundManager.Play("se_battle_hit", priority: SoundPriority.Critical);
```

#### Case 3: New Background Sound (All slots taken by Higher Priority)
```csharp
// If all 32 sounds are High/Critical/UI priority
// This will FAIL and return false
bool success = soundManager.Play("se_ambient", priority: SoundPriority.Background);
// success == false
```

## Looping Sounds

Looping sounds follow the same priority rules:

```csharp
// Low priority ambient loop
var ambientHandle = soundManager.PlayLooping(
    "se_rain",
    volume: 0.4f,
    priority: SoundPriority.Background);

// Can be evicted if higher priority sounds need the slot
```

## Migration Guide

### For Existing Code

All Play methods default to `SoundPriority.Normal`, so existing code continues to work:

```csharp
// Old code - still works, uses Normal priority
soundManager.Play("se_sound");

// Updated code - explicit priority
soundManager.Play("se_sound", priority: SoundPriority.High);
```

### Recommended Priorities

| Sound Category | Recommended Priority |
|----------------|---------------------|
| Menu sounds | UI |
| Error/notification sounds | UI |
| Battle damage/effects | Critical |
| Pokemon cries (battle) | Critical |
| Badge acquisition | Critical |
| Important item pickup | High |
| Menu navigation | High |
| Standard interactions | Normal |
| General item pickup | Normal |
| Footsteps | Low |
| Environmental effects | Low |
| Ambient loops | Background |

## Performance Notes

- The eviction algorithm is O(n) where n = active sounds (max 32)
- Priority comparison is done only when at capacity
- No performance impact during normal operation (< 32 sounds)
- Logging at Debug level shows eviction decisions for tuning

## Testing Priority Behavior

```csharp
// Test priority eviction
var manager = new NAudioSoundEffectManager(registry, maxConcurrentSounds: 3);

// Fill all slots with Low priority
manager.Play("sound1", priority: SoundPriority.Low);    // slot 1
manager.Play("sound2", priority: SoundPriority.Low);    // slot 2
manager.Play("sound3", priority: SoundPriority.Low);    // slot 3

// This will evict sound1 (oldest Low)
manager.Play("sound4", priority: SoundPriority.High);   // slot 1

// This fails - can't evict High with Low
bool result = manager.Play("sound5", priority: SoundPriority.Low);
Assert.False(result);
```
