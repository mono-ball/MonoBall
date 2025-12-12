# Sound Priority System - Implementation Summary

## Overview

Implemented a priority-based sound eviction system for `NAudioSoundEffectManager` to intelligently manage the 32 concurrent sound limit.

## Files Created/Modified

### New Files

1. **`/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Audio/SoundPriority.cs`**
   - Defines the `SoundPriority` enum with 6 priority levels (0-5)
   - Comprehensive XML documentation for each priority level

### Modified Files

1. **`/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Audio/Services/INAudioSoundEffectManager.cs`**
   - Added `priority` parameter to all Play methods with default `SoundPriority.Normal`
   - Updated method signatures:
     - `Play()`
     - `PlayFromFile()`
     - `PlayLooping()`
     - `PlayLoopingFromFile()`

2. **`/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Audio/Services/NAudioSoundEffectManager.cs`**
   - Updated all public Play methods to accept and pass priority parameter
   - Added `Priority` property to `SoundInstance` class
   - Replaced `TryRemoveOldestOneShotSound()` with `TryEvictLowerPrioritySound()`
   - Updated `SoundInstance` constructor to accept priority parameter

## Key Changes

### SoundPriority Enum

```csharp
public enum SoundPriority
{
    Background = 0,  // Ambient sounds, evicted first
    Low = 1,         // Environmental effects
    Normal = 2,      // Standard game sounds (default)
    High = 3,        // Important feedback
    Critical = 4,    // Battle sounds, rarely interrupted
    UI = 5           // UI sounds, never dropped
}
```

### Enhanced SoundInstance

```csharp
private class SoundInstance
{
    public Guid Id { get; }
    public DateTime CreatedAt { get; }
    public bool IsLooping { get; }
    public SoundPriority Priority { get; }  // NEW
    // ...
}
```

### Intelligent Eviction Algorithm

```csharp
private bool TryEvictLowerPrioritySound(SoundPriority incomingPriority)
{
    // Never evict Critical (4) or UI (5) sounds
    if (incomingPriority < SoundPriority.Critical)
    {
        // Find lowest priority sound that is:
        // 1. Below Critical (< 4)
        // 2. Below incoming priority
        // 3. Oldest among sounds of same priority

        // Evict and return true if found
        // Return false if no suitable candidate
    }
    return false;
}
```

## Eviction Rules

1. **Priority First**: Lower priority sounds are evicted before higher priority
2. **Age Second**: Among same priority, oldest sound is evicted
3. **Protection**: Critical and UI sounds are NEVER evicted
4. **Rejection**: If no evictable sound exists, new sound is rejected (returns false/null)

## Priority Matrix

| New Priority | Can Evict | Protected From |
|--------------|-----------|----------------|
| Background   | None      | All |
| Low          | Background | Low and above |
| Normal       | Background, Low | Normal and above |
| High         | Background, Low, Normal | High and above |
| Critical     | Background, Low, Normal, High | Critical, UI |
| UI           | Background, Low, Normal, High | Critical, UI |

## API Changes

### Backward Compatibility

All changes are **backward compatible** due to default parameters:

```csharp
// Old code - still works (uses Normal priority)
manager.Play("se_sound");

// New code - explicit priority
manager.Play("se_sound", priority: SoundPriority.Critical);
```

### Method Signatures

```csharp
// Interface
bool Play(string trackId, float volume = 1.0f, float pitch = 0.0f,
          float pan = 0.0f, SoundPriority priority = SoundPriority.Normal);

bool PlayFromFile(string filePath, float volume = 1.0f, float pitch = 0.0f,
                  float pan = 0.0f, SoundPriority priority = SoundPriority.Normal);

ILoopingSoundHandle? PlayLooping(string trackId, float volume = 1.0f,
                                 float pitch = 0.0f, float pan = 0.0f,
                                 SoundPriority priority = SoundPriority.Normal);

ILoopingSoundHandle? PlayLoopingFromFile(string filePath, float volume = 1.0f,
                                         float pitch = 0.0f, float pan = 0.0f,
                                         SoundPriority priority = SoundPriority.Normal);
```

## Performance Characteristics

- **Time Complexity**: O(n) where n = active sounds (max 32)
- **Memory Overhead**: 4 bytes per sound (enum storage)
- **No Impact**: When below capacity (<32 sounds), no priority checks occur
- **Logging**: Debug-level logging for eviction decisions

## Testing Recommendations

```csharp
[Test]
public void HighPriorityShouldEvictLowPriority()
{
    var manager = new NAudioSoundEffectManager(registry, maxConcurrentSounds: 2);

    manager.Play("sound1", priority: SoundPriority.Low);
    manager.Play("sound2", priority: SoundPriority.Low);

    // Should evict oldest Low priority sound
    bool result = manager.Play("sound3", priority: SoundPriority.High);
    Assert.True(result);
}

[Test]
public void CriticalSoundsCannotBeEvicted()
{
    var manager = new NAudioSoundEffectManager(registry, maxConcurrentSounds: 2);

    manager.Play("sound1", priority: SoundPriority.Critical);
    manager.Play("sound2", priority: SoundPriority.Critical);

    // Should fail - can't evict Critical sounds
    bool result = manager.Play("sound3", priority: SoundPriority.Normal);
    Assert.False(result);
}

[Test]
public void OldestSoundEvictedWithinSamePriority()
{
    var manager = new NAudioSoundEffectManager(registry, maxConcurrentSounds: 3);

    manager.Play("sound1", priority: SoundPriority.Normal);  // Oldest Normal
    Thread.Sleep(10);
    manager.Play("sound2", priority: SoundPriority.Normal);
    Thread.Sleep(10);
    manager.Play("sound3", priority: SoundPriority.Normal);

    // Should evict sound1 (oldest Normal)
    manager.Play("sound4", priority: SoundPriority.High);
}
```

## Usage Examples

### Battle System
```csharp
// Critical priority for battle sounds
battleAudio.PlayMove("se_tackle", priority: SoundPriority.Critical);
battleAudio.PlayCry(pokemonId, priority: SoundPriority.Critical);
battleAudio.PlayDamage("se_hit", priority: SoundPriority.Critical);
```

### UI System
```csharp
// UI priority for interface sounds
uiAudio.PlayConfirm(priority: SoundPriority.UI);
uiAudio.PlayCancel(priority: SoundPriority.UI);
uiAudio.PlayError(priority: SoundPriority.UI);
```

### Ambient System
```csharp
// Background priority for ambient sounds
ambientAudio.PlayWind(priority: SoundPriority.Background);
ambientAudio.PlayWater(priority: SoundPriority.Background);
ambientAudio.PlayBirds(priority: SoundPriority.Background);
```

### Item System
```csharp
// Variable priority based on item importance
var priority = item.IsKeyItem ? SoundPriority.High : SoundPriority.Normal;
itemAudio.PlayPickup(priority: priority);
```

## Benefits

1. **No More Random Eviction**: Intelligent priority-based selection
2. **Protected Critical Sounds**: Battle and UI sounds never interrupted
3. **Backward Compatible**: Existing code continues to work
4. **Performance**: No overhead when below capacity
5. **Flexibility**: Game designers can tune priorities per sound
6. **Logging**: Debug output helps tune priorities during development

## Future Enhancements

Possible future improvements:
1. **Per-Category Priority Defaults**: Set default priorities by sound category
2. **Dynamic Priority Adjustment**: Adjust priority based on game state
3. **Priority Decay**: Lower priority of long-playing sounds over time
4. **Group Eviction**: Evict groups of related sounds together
5. **Statistics**: Track eviction patterns for balancing

## Migration Checklist

- [x] Create `SoundPriority` enum
- [x] Update interface signatures with priority parameter
- [x] Implement priority-based eviction algorithm
- [x] Update `SoundInstance` to track priority
- [x] Ensure backward compatibility with defaults
- [x] Add comprehensive documentation
- [ ] Update calling code to use appropriate priorities
- [ ] Test priority behavior in different scenarios
- [ ] Performance testing with full 32 concurrent sounds
- [ ] Balance priority levels based on playtesting feedback
