# Audio System Architecture Design for PokeSharp

**Date:** December 10, 2025
**Status:** Architecture Analysis & Design
**Target Framework:** MonoBallFramework (Arch ECS + MonoGame)

---

## Executive Summary

This document provides a comprehensive architecture design for PokeSharp's audio system, following established patterns from the existing codebase including the Event Bus, ECS Systems, Service Pattern, and Component-based architecture.

**Design Goals:**
- Full integration with existing Arch ECS architecture
- Event-driven audio triggers
- Pokemon-specific audio management (800+ cries, battle audio, world zones)
- Memory-efficient resource pooling
- Service-based abstraction over MonoGame audio APIs
- Support for BGM, SFX, Voice, and Ambient audio categories

---

## 1. Audio System Architecture Overview

### 1.1 High-Level Component Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Audio System Layers                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │         Application Layer (Game Systems)                 │   │
│  │  - GameplayScene                                         │   │
│  │  - BattleSystem                                          │   │
│  │  - MapStreamingSystem                                    │   │
│  │  - PokemonSystem                                         │   │
│  └───────────────────────┬─────────────────────────────────┘   │
│                          │ uses                                  │
│  ┌───────────────────────▼─────────────────────────────────┐   │
│  │         Service Layer (IAudioService)                    │   │
│  │  - IAudioService (DI interface)                          │   │
│  │  - AudioService (implementation)                         │   │
│  │  - AudioChannel management                               │   │
│  │  - Volume hierarchy                                      │   │
│  └───────────────────────┬─────────────────────────────────┘   │
│                          │ manages                               │
│  ┌───────────────────────▼─────────────────────────────────┐   │
│  │         ECS Layer (Systems & Components)                 │   │
│  │  - AudioSystem (IUpdateSystem)                           │   │
│  │  - AudioEventSystem (IEventDrivenSystem)                 │   │
│  │  - AudioSource component                                 │   │
│  │  - AudioListener component                               │   │
│  └───────────────────────┬─────────────────────────────────┘   │
│                          │ triggers                              │
│  ┌───────────────────────▼─────────────────────────────────┐   │
│  │         Event Layer (IEventBus)                          │   │
│  │  - MusicChangeRequestedEvent                             │   │
│  │  - SoundEffectRequestedEvent                             │   │
│  │  - PokemonCryRequestedEvent                              │   │
│  │  - BattleAudioStateChangedEvent                          │   │
│  └───────────────────────┬─────────────────────────────────┘   │
│                          │ loads                                 │
│  ┌───────────────────────▼─────────────────────────────────┐   │
│  │         Resource Layer (MonoGame Content)                │   │
│  │  - SoundEffect pooling                                   │   │
│  │  - MediaPlayer for BGM                                   │   │
│  │  - SoundEffectInstance management                        │   │
│  │  - Lazy loading / streaming                              │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. Audio Manager Architecture

### 2.1 Service Pattern vs Singleton

**Decision: Service Pattern with DI (following existing codebase conventions)**

Rationale:
- Existing codebase uses Service Pattern extensively (MapDefinitionService, GameStateService, etc.)
- Supports dependency injection
- Better testability
- Follows SOLID principles
- Consistent with MonoBallFramework architecture

### 2.2 Audio Service Interface

```csharp
/// <summary>
/// Core audio service interface for managing all audio playback.
/// Registered as a singleton service in DI container.
/// </summary>
public interface IAudioService
{
    // === Master Control ===
    float MasterVolume { get; set; }
    bool IsMuted { get; set; }

    // === Category Control ===
    void SetCategoryVolume(AudioCategory category, float volume);
    float GetCategoryVolume(AudioCategory category);
    void MuteCategory(AudioCategory category, bool mute);

    // === Music (BGM) ===
    void PlayMusic(string trackId, bool loop = true, float fadeInSeconds = 0f);
    void StopMusic(float fadeOutSeconds = 0f);
    void PauseMusic();
    void ResumeMusic();
    string? CurrentMusicTrack { get; }

    // === Sound Effects ===
    AudioHandle PlaySound(string soundId, AudioSourceConfig? config = null);
    void StopSound(AudioHandle handle);
    void StopAllSounds(AudioCategory? category = null);

    // === Pokemon Cries ===
    AudioHandle PlayPokemonCry(int pokemonId, AudioSourceConfig? config = null);
    AudioHandle PlayPokemonCry(string pokemonName, AudioSourceConfig? config = null);

    // === Spatial Audio (3D positioning) ===
    AudioHandle PlaySoundAtPosition(string soundId, Vector2 position, float maxDistance);
    void UpdateListenerPosition(Vector2 position);

    // === Resource Management ===
    void PreloadSound(string soundId);
    void PreloadSounds(IEnumerable<string> soundIds);
    void UnloadSound(string soundId);
    void UnloadUnusedSounds();

    // === State Queries ===
    bool IsSoundPlaying(AudioHandle handle);
    int GetActiveSoundCount(AudioCategory? category = null);
}

/// <summary>
/// Audio categories for volume and mixing control.
/// </summary>
public enum AudioCategory
{
    Master,
    Music,       // BGM tracks
    SoundEffect, // General SFX
    Voice,       // NPC dialogue, player voice
    Ambient,     // Environmental loops
    UI,          // Menu sounds, notifications
    Pokemon      // Pokemon cries
}

/// <summary>
/// Configuration for audio source playback.
/// </summary>
public struct AudioSourceConfig
{
    public float Volume { get; set; }      // 0.0 to 1.0
    public float Pitch { get; set; }       // 0.5 to 2.0 (default 1.0)
    public float Pan { get; set; }         // -1.0 (left) to 1.0 (right)
    public bool Loop { get; set; }
    public AudioCategory Category { get; set; }

    public static AudioSourceConfig Default => new()
    {
        Volume = 1.0f,
        Pitch = 1.0f,
        Pan = 0.0f,
        Loop = false,
        Category = AudioCategory.SoundEffect
    };
}

/// <summary>
/// Handle for tracking and controlling individual audio instances.
/// Uses struct for performance (no heap allocation).
/// </summary>
public readonly struct AudioHandle
{
    public readonly int Id;
    public readonly AudioCategory Category;

    public bool IsValid => Id > 0;

    public static AudioHandle Invalid => new(0, AudioCategory.Master);

    public AudioHandle(int id, AudioCategory category)
    {
        Id = id;
        Category = category;
    }
}
```

---

## 3. Resource Management Architecture

### 3.1 Loading Strategy Matrix

| Audio Type | Strategy | Rationale |
|------------|----------|-----------|
| **BGM Tracks** | Streaming | Large files (2-5 MB), only 1 active |
| **Common SFX** | Preload | Small files (<100 KB), high frequency |
| **Pokemon Cries** | Lazy Load + Pool | 800+ sounds, predictable usage |
| **Battle SFX** | Preload on Battle Start | Known set, confined scope |
| **World Ambient** | Stream | Long loops, map-specific |
| **UI Sounds** | Preload at Startup | Critical path, instant feedback |

### 3.2 Sound Pooling Architecture

```csharp
/// <summary>
/// Pool manager for SoundEffectInstance objects to reduce GC pressure.
/// Uses object pooling pattern similar to EventPool in existing codebase.
/// </summary>
public class AudioInstancePool
{
    private readonly Dictionary<string, Queue<SoundEffectInstance>> _pools;
    private readonly Dictionary<string, SoundEffect> _loadedSounds;
    private readonly int _maxPoolSize;

    public AudioInstancePool(int maxPoolSizePerSound = 5)
    {
        _pools = new Dictionary<string, Queue<SoundEffectInstance>>(128);
        _loadedSounds = new Dictionary<string, SoundEffect>(128);
        _maxPoolSize = maxPoolSizePerSound;
    }

    public SoundEffectInstance Rent(string soundId)
    {
        if (!_pools.TryGetValue(soundId, out var pool))
        {
            pool = new Queue<SoundEffectInstance>(_maxPoolSize);
            _pools[soundId] = pool;
        }

        // Reuse stopped instance if available
        while (pool.Count > 0)
        {
            var instance = pool.Dequeue();
            if (instance.State == SoundState.Stopped)
            {
                return instance;
            }
            instance.Dispose(); // Cleanup invalid instances
        }

        // Create new instance
        if (!_loadedSounds.TryGetValue(soundId, out var soundEffect))
        {
            throw new InvalidOperationException($"Sound not loaded: {soundId}");
        }

        return soundEffect.CreateInstance();
    }

    public void Return(string soundId, SoundEffectInstance instance)
    {
        if (instance.State != SoundState.Stopped)
        {
            instance.Stop(immediate: true);
        }

        if (_pools.TryGetValue(soundId, out var pool) && pool.Count < _maxPoolSize)
        {
            pool.Enqueue(instance);
        }
        else
        {
            instance.Dispose();
        }
    }

    public void LoadSound(string soundId, SoundEffect soundEffect)
    {
        _loadedSounds[soundId] = soundEffect;
    }

    public void UnloadSound(string soundId)
    {
        if (_pools.TryGetValue(soundId, out var pool))
        {
            while (pool.Count > 0)
            {
                pool.Dequeue().Dispose();
            }
            _pools.Remove(soundId);
        }

        if (_loadedSounds.TryGetValue(soundId, out var sound))
        {
            sound.Dispose();
            _loadedSounds.Remove(soundId);
        }
    }
}
```

### 3.3 Memory Budget Recommendations

```
Total Audio Budget: ~50-100 MB (configurable)

Breakdown:
- BGM (streaming):     5-10 MB active memory
- Common SFX (loaded): 10-20 MB
- Pokemon Cries (pool): 15-30 MB (lazy loaded, ~200 most common)
- Battle Audio:        5-10 MB
- Ambient:            5-10 MB (streaming)
- UI Sounds:          1-2 MB
- Buffer/Overhead:    10-20 MB
```

---

## 4. Pokemon-Specific Audio Requirements

### 4.1 Pokemon Cry Management

```csharp
/// <summary>
/// Service for managing Pokemon cry audio files.
/// Handles 800+ Pokemon cries with intelligent caching.
/// </summary>
public interface IPokemonCryService
{
    // Preload frequently encountered Pokemon cries
    void PreloadCommonCries(int[] pokemonIds);

    // Get cry audio ID by Pokemon ID or name
    string GetCryAudioId(int pokemonId);
    string GetCryAudioId(string pokemonName);

    // Play cry with auto-loading
    AudioHandle PlayCry(int pokemonId, AudioSourceConfig? config = null);

    // Cleanup unused cries
    void UnloadUnusedCries(TimeSpan unusedDuration);
}

/// <summary>
/// Component for tracking Pokemon cry usage and caching.
/// Attached to Pokemon entities.
/// </summary>
public struct PokemonAudioData
{
    public int PokemonId { get; set; }
    public string CryAudioId { get; set; }
    public DateTime LastCryPlayed { get; set; }
}
```

**Cry Loading Strategy:**
1. **Startup:** Preload starter Pokemon and common route Pokemon (25 cries)
2. **On Route Change:** Preload cries for Pokemon in current encounter zone
3. **On Battle Start:** Ensure both Pokemon cries are loaded
4. **Lazy Load:** Load on-demand for other Pokemon
5. **Cache Eviction:** Unload cries unused for 5+ minutes

### 4.2 Battle Audio Orchestration

```csharp
/// <summary>
/// State machine for battle audio management.
/// Coordinates BGM, SFX, and cries during battle.
/// </summary>
public class BattleAudioOrchestrator
{
    private readonly IAudioService _audioService;
    private readonly IEventBus _eventBus;

    private BattleAudioState _currentState;
    private string? _battleMusicTrack;
    private string? _previousWorldMusic;

    public BattleAudioOrchestrator(IAudioService audioService, IEventBus eventBus)
    {
        _audioService = audioService;
        _eventBus = eventBus;

        // Subscribe to battle events
        _eventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        _eventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
        _eventBus.Subscribe<PokemonFaintedEvent>(OnPokemonFainted);
        _eventBus.Subscribe<MoveUsedEvent>(OnMoveUsed);
    }

    private void OnBattleStarted(BattleStartedEvent evt)
    {
        // Save current world music
        _previousWorldMusic = _audioService.CurrentMusicTrack;

        // Determine battle music based on battle type
        _battleMusicTrack = GetBattleMusicTrack(evt.BattleType, evt.TrainerType);

        // Crossfade to battle music
        _audioService.StopMusic(fadeOutSeconds: 0.5f);
        _audioService.PlayMusic(_battleMusicTrack, loop: true, fadeInSeconds: 0.5f);

        // Preload battle SFX
        PreloadBattleSounds(evt.BattleType);

        _currentState = BattleAudioState.BattleMusic;
    }

    private void OnBattleEnded(BattleEndedEvent evt)
    {
        // Play victory/defeat jingle
        if (evt.Victory)
        {
            _audioService.StopMusic(fadeOutSeconds: 0.2f);
            _audioService.PlayMusic("victory_jingle", loop: false);
            _currentState = BattleAudioState.VictoryJingle;
        }
        else
        {
            _audioService.StopMusic(fadeOutSeconds: 1.0f);
            _currentState = BattleAudioState.Defeat;
        }
    }

    public void OnVictoryJingleComplete()
    {
        // Resume world music
        if (_previousWorldMusic != null)
        {
            _audioService.PlayMusic(_previousWorldMusic, fadeInSeconds: 0.5f);
        }
        _currentState = BattleAudioState.None;
    }

    private string GetBattleMusicTrack(BattleType type, TrainerType? trainerType)
    {
        return type switch
        {
            BattleType.WildPokemon => "battle_wild",
            BattleType.Trainer when trainerType == TrainerType.GymLeader => "battle_gym_leader",
            BattleType.Trainer when trainerType == TrainerType.Elite4 => "battle_elite4",
            BattleType.Trainer when trainerType == TrainerType.Champion => "battle_champion",
            BattleType.Trainer => "battle_trainer",
            _ => "battle_wild"
        };
    }
}

public enum BattleAudioState
{
    None,
    BattleMusic,
    VictoryJingle,
    Defeat,
    LowHealth // When Pokemon HP < 25%, music changes
}
```

### 4.3 World Audio Zones

```csharp
/// <summary>
/// Component attached to map entities to define audio environment.
/// Similar to existing Weather, Music components.
/// </summary>
public struct AudioZone
{
    public string MusicTrackId { get; set; }
    public string? AmbientLoopId { get; set; }
    public float AmbientVolume { get; set; }
    public bool ContinueMusicAcrossMaps { get; set; } // For connected areas
}

/// <summary>
/// System that manages audio zone transitions during map streaming.
/// Subscribes to MapTransitionEvent from existing MapStreamingSystem.
/// </summary>
public class AudioZoneSystem : SystemBase, IEventDrivenSystem
{
    private readonly IAudioService _audioService;
    private readonly IEventBus _eventBus;
    private string? _currentMusicZone;

    public AudioZoneSystem(IAudioService audioService, IEventBus eventBus)
    {
        _audioService = audioService;
        _eventBus = eventBus;
    }

    protected override void OnInitialized()
    {
        _eventBus.Subscribe<MapTransitionEvent>(OnMapTransition);
    }

    private void OnMapTransition(MapTransitionEvent evt)
    {
        // Query for AudioZone component on new map
        var query = new QueryDescription().WithAll<MapInfo, AudioZone>();

        World.Query(in query, (ref MapInfo mapInfo, ref AudioZone audioZone) =>
        {
            if (mapInfo.MapId == evt.ToMapId)
            {
                HandleAudioZoneTransition(ref audioZone);
            }
        });
    }

    private void HandleAudioZoneTransition(ref AudioZone newZone)
    {
        // Check if music should continue
        if (newZone.ContinueMusicAcrossMaps &&
            _audioService.CurrentMusicTrack == newZone.MusicTrackId)
        {
            // Keep current music playing
            return;
        }

        // Crossfade to new zone music
        if (_currentMusicZone != newZone.MusicTrackId)
        {
            _audioService.StopMusic(fadeOutSeconds: 1.0f);
            _audioService.PlayMusic(newZone.MusicTrackId, fadeInSeconds: 1.0f);
            _currentMusicZone = newZone.MusicTrackId;
        }

        // Update ambient audio
        if (!string.IsNullOrEmpty(newZone.AmbientLoopId))
        {
            var ambientConfig = new AudioSourceConfig
            {
                Volume = newZone.AmbientVolume,
                Loop = true,
                Category = AudioCategory.Ambient
            };
            _audioService.PlaySound(newZone.AmbientLoopId, ambientConfig);
        }
    }
}
```

---

## 5. Design Patterns & Architecture

### 5.1 Observer Pattern - Event-Driven Audio

```csharp
/// <summary>
/// Audio events following existing IGameEvent pattern.
/// Use PublishPooled for high-frequency events.
/// </summary>

public class MusicChangeRequestedEvent : GameEventBase, IPoolableEvent
{
    public string TrackId { get; set; } = string.Empty;
    public bool Loop { get; set; } = true;
    public float FadeInSeconds { get; set; }

    public void Reset()
    {
        TrackId = string.Empty;
        Loop = true;
        FadeInSeconds = 0f;
    }
}

public class SoundEffectRequestedEvent : GameEventBase, IPoolableEvent
{
    public string SoundId { get; set; } = string.Empty;
    public AudioSourceConfig Config { get; set; }
    public Vector2? Position { get; set; } // For spatial audio

    public void Reset()
    {
        SoundId = string.Empty;
        Config = AudioSourceConfig.Default;
        Position = null;
    }
}

public class PokemonCryRequestedEvent : GameEventBase, IPoolableEvent
{
    public int PokemonId { get; set; }
    public AudioSourceConfig Config { get; set; }

    public void Reset()
    {
        PokemonId = 0;
        Config = AudioSourceConfig.Default;
    }
}

public class AudioVolumeChangedEvent : GameEventBase, IPoolableEvent
{
    public AudioCategory Category { get; set; }
    public float NewVolume { get; set; }

    public void Reset()
    {
        Category = AudioCategory.Master;
        NewVolume = 1.0f;
    }
}
```

### 5.2 Command Pattern - Audio Actions

```csharp
/// <summary>
/// Command pattern for audio actions that need undo/redo capability.
/// Useful for audio scripting and cutscenes.
/// </summary>
public interface IAudioCommand
{
    void Execute(IAudioService audioService);
    void Undo(IAudioService audioService);
}

public class PlayMusicCommand : IAudioCommand
{
    private readonly string _trackId;
    private readonly string? _previousTrack;

    public PlayMusicCommand(string trackId, string? previousTrack = null)
    {
        _trackId = trackId;
        _previousTrack = previousTrack;
    }

    public void Execute(IAudioService audioService)
    {
        audioService.PlayMusic(_trackId, fadeInSeconds: 0.5f);
    }

    public void Undo(IAudioService audioService)
    {
        if (_previousTrack != null)
        {
            audioService.PlayMusic(_previousTrack, fadeInSeconds: 0.5f);
        }
    }
}

public class SetVolumeCommand : IAudioCommand
{
    private readonly AudioCategory _category;
    private readonly float _newVolume;
    private float _previousVolume;

    public SetVolumeCommand(AudioCategory category, float newVolume)
    {
        _category = category;
        _newVolume = newVolume;
    }

    public void Execute(IAudioService audioService)
    {
        _previousVolume = audioService.GetCategoryVolume(_category);
        audioService.SetCategoryVolume(_category, _newVolume);
    }

    public void Undo(IAudioService audioService)
    {
        audioService.SetCategoryVolume(_category, _previousVolume);
    }
}
```

### 5.3 State Pattern - Audio Modes

```csharp
/// <summary>
/// State machine for global audio modes.
/// Similar to SceneState in existing codebase.
/// </summary>
public enum AudioMode
{
    Normal,      // Standard gameplay
    Battle,      // Battle music and SFX priority
    Cutscene,    // Scripted audio sequence
    Menu,        // UI sounds only, BGM dimmed
    Muted        // All audio stopped
}

public interface IAudioModeState
{
    void Enter(IAudioService audioService);
    void Exit(IAudioService audioService);
    void Update(float deltaTime);
}

public class BattleAudioMode : IAudioModeState
{
    public void Enter(IAudioService audioService)
    {
        // Reduce ambient volume
        audioService.SetCategoryVolume(AudioCategory.Ambient, 0.3f);
        // Full volume for Pokemon cries and battle SFX
        audioService.SetCategoryVolume(AudioCategory.Pokemon, 1.0f);
        audioService.SetCategoryVolume(AudioCategory.SoundEffect, 1.0f);
    }

    public void Exit(IAudioService audioService)
    {
        // Restore ambient volume
        audioService.SetCategoryVolume(AudioCategory.Ambient, 0.7f);
    }

    public void Update(float deltaTime) { }
}

public class MenuAudioMode : IAudioModeState
{
    private float _originalMusicVolume;

    public void Enter(IAudioService audioService)
    {
        _originalMusicVolume = audioService.GetCategoryVolume(AudioCategory.Music);
        // Dim BGM when in menus
        audioService.SetCategoryVolume(AudioCategory.Music, _originalMusicVolume * 0.5f);
    }

    public void Exit(IAudioService audioService)
    {
        audioService.SetCategoryVolume(AudioCategory.Music, _originalMusicVolume);
    }

    public void Update(float deltaTime) { }
}
```

### 5.4 Factory Pattern - Sound Creation

```csharp
/// <summary>
/// Factory for creating configured audio sources.
/// Encapsulates complex audio setup logic.
/// </summary>
public interface IAudioSourceFactory
{
    AudioHandle CreateBattleSound(string soundId);
    AudioHandle CreateUISound(string soundId);
    AudioHandle CreatePokemonCry(int pokemonId);
    AudioHandle CreateSpatialSound(string soundId, Vector2 position, float maxDistance);
}

public class AudioSourceFactory : IAudioSourceFactory
{
    private readonly IAudioService _audioService;

    public AudioSourceFactory(IAudioService audioService)
    {
        _audioService = audioService;
    }

    public AudioHandle CreateBattleSound(string soundId)
    {
        var config = new AudioSourceConfig
        {
            Category = AudioCategory.SoundEffect,
            Volume = 0.8f,
            Pitch = 1.0f
        };
        return _audioService.PlaySound(soundId, config);
    }

    public AudioHandle CreateUISound(string soundId)
    {
        var config = new AudioSourceConfig
        {
            Category = AudioCategory.UI,
            Volume = 1.0f,
            Pitch = 1.0f
        };
        return _audioService.PlaySound(soundId, config);
    }

    public AudioHandle CreatePokemonCry(int pokemonId)
    {
        return _audioService.PlayPokemonCry(pokemonId, new AudioSourceConfig
        {
            Category = AudioCategory.Pokemon,
            Volume = 0.9f,
            Pitch = 1.0f
        });
    }

    public AudioHandle CreateSpatialSound(string soundId, Vector2 position, float maxDistance)
    {
        return _audioService.PlaySoundAtPosition(soundId, position, maxDistance);
    }
}
```

---

## 6. ECS Integration

### 6.1 Audio Components

```csharp
/// <summary>
/// Tag component for the main audio listener (typically the player/camera).
/// Only one AudioListener should exist at a time.
/// </summary>
public struct AudioListener
{
    // Tag component - no data needed
}

/// <summary>
/// Component for entities that emit audio (NPCs, Pokemon, objects).
/// </summary>
public struct AudioSource
{
    public string CurrentSoundId { get; set; }
    public AudioHandle Handle { get; set; }
    public bool IsPlaying { get; set; }
    public float Volume { get; set; }
    public AudioCategory Category { get; set; }
}

/// <summary>
/// Component for looping ambient sounds attached to zones or objects.
/// </summary>
public struct AmbientAudioEmitter
{
    public string SoundId { get; set; }
    public AudioHandle Handle { get; set; }
    public float Radius { get; set; }        // Max audible distance
    public float VolumeAtCenter { get; set; } // Volume at origin
    public bool IsActive { get; set; }
}

/// <summary>
/// Component for tracking audio state on Pokemon entities.
/// </summary>
public struct PokemonAudio
{
    public int PokemonId { get; set; }
    public string CryAudioId { get; set; }
    public AudioHandle LastCryHandle { get; set; }
}

/// <summary>
/// Component for music tracks associated with maps.
/// Already exists in codebase as Music.cs - can be extended.
/// </summary>
public struct Music
{
    public string Value { get; set; }
    public bool ContinueAcrossMaps { get; set; } // New field
    public float FadeInTime { get; set; }        // New field
}
```

### 6.2 Audio Systems

```csharp
/// <summary>
/// Core audio system that processes audio events and updates audio state.
/// Runs every frame to handle spatial audio, crossfades, etc.
/// </summary>
public class AudioSystem : SystemBase, IUpdateSystem, IEventDrivenSystem
{
    private readonly IAudioService _audioService;
    private readonly IEventBus _eventBus;

    private QueryDescription _audioSourceQuery;
    private QueryDescription _audioListenerQuery;
    private QueryDescription _ambientEmitterQuery;

    public override int Priority => SystemPriority.Audio; // 500 - After gameplay, before rendering

    public AudioSystem(IAudioService audioService, IEventBus eventBus)
    {
        _audioService = audioService;
        _eventBus = eventBus;
    }

    protected override void OnInitialized()
    {
        _audioSourceQuery = new QueryDescription().WithAll<AudioSource, Position>();
        _audioListenerQuery = new QueryDescription().WithAll<AudioListener, Position>();
        _ambientEmitterQuery = new QueryDescription().WithAll<AmbientAudioEmitter, Position>();

        // Subscribe to audio events
        _eventBus.Subscribe<MusicChangeRequestedEvent>(OnMusicChangeRequested);
        _eventBus.Subscribe<SoundEffectRequestedEvent>(OnSoundEffectRequested);
        _eventBus.Subscribe<PokemonCryRequestedEvent>(OnPokemonCryRequested);
    }

    public override void Update(World world, float deltaTime)
    {
        if (!Enabled) return;

        // Update audio listener position for spatial audio
        UpdateListenerPosition(world);

        // Update spatial audio sources
        UpdateSpatialAudio(world, deltaTime);

        // Update ambient emitters
        UpdateAmbientEmitters(world, deltaTime);

        // Cleanup finished sounds
        CleanupFinishedSounds(world);
    }

    private void UpdateListenerPosition(World world)
    {
        world.Query(in _audioListenerQuery, (ref AudioListener listener, ref Position position) =>
        {
            _audioService.UpdateListenerPosition(new Vector2(position.PixelX, position.PixelY));
        });
    }

    private void UpdateSpatialAudio(World world, float deltaTime)
    {
        // Get listener position
        Vector2? listenerPos = null;
        world.Query(in _audioListenerQuery, (ref AudioListener _, ref Position position) =>
        {
            listenerPos = new Vector2(position.PixelX, position.PixelY);
        });

        if (!listenerPos.HasValue) return;

        // Update all audio sources based on distance from listener
        world.Query(in _audioSourceQuery, (ref AudioSource source, ref Position position) =>
        {
            if (!source.IsPlaying || !source.Handle.IsValid)
                return;

            float distance = Vector2.Distance(listenerPos.Value,
                new Vector2(position.PixelX, position.PixelY));

            // Apply distance-based volume attenuation
            // Max audible distance: 10 tiles (160 pixels)
            float maxDistance = 160f;
            float volumeMultiplier = Math.Max(0, 1.0f - (distance / maxDistance));

            // Update sound volume (would need extension to IAudioService)
            // For now, this is conceptual - requires SoundEffectInstance access
        });
    }

    private void UpdateAmbientEmitters(World world, float deltaTime)
    {
        world.Query(in _ambientEmitterQuery,
            (ref AmbientAudioEmitter emitter, ref Position position) =>
        {
            if (!emitter.IsActive && !string.IsNullOrEmpty(emitter.SoundId))
            {
                // Start ambient loop
                var config = new AudioSourceConfig
                {
                    Loop = true,
                    Volume = emitter.VolumeAtCenter,
                    Category = AudioCategory.Ambient
                };
                emitter.Handle = _audioService.PlaySound(emitter.SoundId, config);
                emitter.IsActive = true;
            }
        });
    }

    private void CleanupFinishedSounds(World world)
    {
        world.Query(in _audioSourceQuery, (ref AudioSource source) =>
        {
            if (source.Handle.IsValid && !_audioService.IsSoundPlaying(source.Handle))
            {
                source.Handle = AudioHandle.Invalid;
                source.IsPlaying = false;
            }
        });
    }

    // Event handlers
    private void OnMusicChangeRequested(MusicChangeRequestedEvent evt)
    {
        _audioService.PlayMusic(evt.TrackId, evt.Loop, evt.FadeInSeconds);
    }

    private void OnSoundEffectRequested(SoundEffectRequestedEvent evt)
    {
        if (evt.Position.HasValue)
        {
            _audioService.PlaySoundAtPosition(evt.SoundId, evt.Position.Value, 160f);
        }
        else
        {
            _audioService.PlaySound(evt.SoundId, evt.Config);
        }
    }

    private void OnPokemonCryRequested(PokemonCryRequestedEvent evt)
    {
        _audioService.PlayPokemonCry(evt.PokemonId, evt.Config);
    }
}

/// <summary>
/// System priority constant for audio (following existing pattern).
/// </summary>
public static partial class SystemPriority
{
    public const int Audio = 500; // After gameplay (100-400), before rendering (600+)
}
```

---

## 7. UML Class Diagrams (Text Format)

### 7.1 Core Service Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     <<interface>>                            │
│                     IAudioService                            │
├─────────────────────────────────────────────────────────────┤
│ + MasterVolume: float                                        │
│ + IsMuted: bool                                              │
│ + CurrentMusicTrack: string?                                 │
├─────────────────────────────────────────────────────────────┤
│ + PlayMusic(trackId, loop, fade): void                       │
│ + StopMusic(fadeOut): void                                   │
│ + PlaySound(soundId, config): AudioHandle                    │
│ + PlayPokemonCry(id, config): AudioHandle                    │
│ + SetCategoryVolume(category, volume): void                  │
│ + PreloadSound(soundId): void                                │
│ + UpdateListenerPosition(position): void                     │
└─────────────────────────────────────────────────────────────┘
                          △
                          │ implements
                          │
┌─────────────────────────▼─────────────────────────────────┐
│                    AudioService                            │
├────────────────────────────────────────────────────────────┤
│ - _audioChannels: Dictionary<AudioCategory, AudioChannel> │
│ - _instancePool: AudioInstancePool                         │
│ - _musicPlayer: MusicPlayer                                │
│ - _pokemonCryService: IPokemonCryService                   │
│ - _activeSounds: Dictionary<int, ActiveSound>              │
│ - _nextHandleId: int                                       │
│ - _masterVolume: float                                     │
│ - _isMuted: bool                                           │
├────────────────────────────────────────────────────────────┤
│ + AudioService(contentManager, pokemonCryService)          │
│ + PlayMusic(trackId, loop, fade): void                     │
│ + StopMusic(fadeOut): void                                 │
│ + PlaySound(soundId, config): AudioHandle                  │
│ + PlayPokemonCry(id, config): AudioHandle                  │
│ - CreateHandle(category): AudioHandle                      │
│ - ApplyVolumeHierarchy(handle, baseVolume): void           │
└────────────────────────────────────────────────────────────┘
                          │
                          │ has-a
                          ▼
┌────────────────────────────────────────────────────────────┐
│                   AudioChannel                              │
├────────────────────────────────────────────────────────────┤
│ + Category: AudioCategory                                   │
│ + Volume: float                                             │
│ + IsMuted: bool                                             │
│ + ActiveSounds: List<AudioHandle>                           │
├────────────────────────────────────────────────────────────┤
│ + AddSound(handle): void                                    │
│ + RemoveSound(handle): void                                 │
│ + StopAll(): void                                           │
└────────────────────────────────────────────────────────────┘
```

### 7.2 Pokemon Cry System

```
┌─────────────────────────────────────────────────────────────┐
│              <<interface>>                                   │
│              IPokemonCryService                              │
├─────────────────────────────────────────────────────────────┤
│ + GetCryAudioId(pokemonId): string                           │
│ + GetCryAudioId(pokemonName): string                         │
│ + PreloadCommonCries(pokemonIds): void                       │
│ + PlayCry(pokemonId, config): AudioHandle                    │
│ + UnloadUnusedCries(unusedDuration): void                    │
└─────────────────────────────────────────────────────────────┘
                          △
                          │
                          │ implements
┌─────────────────────────▼─────────────────────────────────┐
│                PokemonCryService                            │
├────────────────────────────────────────────────────────────┤
│ - _audioService: IAudioService                              │
│ - _contentManager: ContentManager                           │
│ - _cryIdMap: Dictionary<int, string>                        │
│ - _cryNameMap: Dictionary<string, string>                   │
│ - _loadedCries: Dictionary<string, DateTime>                │
│ - _commonCryIds: HashSet<int>                               │
├────────────────────────────────────────────────────────────┤
│ + PokemonCryService(audioService, contentManager)          │
│ + GetCryAudioId(pokemonId): string                          │
│ + PreloadCommonCries(pokemonIds): void                      │
│ + PlayCry(pokemonId, config): AudioHandle                   │
│ - LoadCryMetadata(): void                                   │
│ - GetCryFilePath(pokemonId): string                         │
└────────────────────────────────────────────────────────────┘
                          │
                          │ uses
                          ▼
┌────────────────────────────────────────────────────────────┐
│                   PokemonAudio                              │
│                   (ECS Component)                           │
├────────────────────────────────────────────────────────────┤
│ + PokemonId: int                                            │
│ + CryAudioId: string                                        │
│ + LastCryHandle: AudioHandle                                │
└────────────────────────────────────────────────────────────┘
```

### 7.3 Battle Audio State Machine

```
┌────────────────────────────────────────────────────────────┐
│              BattleAudioOrchestrator                        │
├────────────────────────────────────────────────────────────┤
│ - _audioService: IAudioService                              │
│ - _eventBus: IEventBus                                      │
│ - _currentState: BattleAudioState                           │
│ - _battleMusicTrack: string?                                │
│ - _previousWorldMusic: string?                              │
├────────────────────────────────────────────────────────────┤
│ + BattleAudioOrchestrator(audioService, eventBus)          │
│ - OnBattleStarted(event): void                              │
│ - OnBattleEnded(event): void                                │
│ - OnPokemonFainted(event): void                             │
│ - GetBattleMusicTrack(type, trainer): string                │
│ - PreloadBattleSounds(battleType): void                     │
│ + OnVictoryJingleComplete(): void                           │
└────────────────────────────────────────────────────────────┘
                          │
                          │ manages state
                          ▼
┌────────────────────────────────────────────────────────────┐
│              <<enumeration>>                                │
│              BattleAudioState                               │
├────────────────────────────────────────────────────────────┤
│ None                                                         │
│ BattleMusic                                                  │
│ VictoryJingle                                                │
│ Defeat                                                       │
│ LowHealth                                                    │
└────────────────────────────────────────────────────────────┘
                          △
                          │
                          │ subscribes
                          │
┌─────────────────────────▼─────────────────────────────────┐
│                   IEventBus                                 │
├────────────────────────────────────────────────────────────┤
│ + Subscribe<T>(handler): IDisposable                        │
│ + Publish<T>(event): void                                   │
│ + PublishPooled<T>(configure): void                         │
└────────────────────────────────────────────────────────────┘
                          │
                          │ publishes
                          ▼
┌────────────────────────────────────────────────────────────┐
│         Battle Audio Events (all pooled)                    │
├────────────────────────────────────────────────────────────┤
│ • BattleStartedEvent                                        │
│ • BattleEndedEvent                                          │
│ • PokemonFaintedEvent                                       │
│ • MoveUsedEvent                                             │
│ • LowHealthWarningEvent                                     │
└────────────────────────────────────────────────────────────┘
```

### 7.4 Audio Zone System

```
┌────────────────────────────────────────────────────────────┐
│              AudioZoneSystem                                │
│              : SystemBase, IEventDrivenSystem               │
├────────────────────────────────────────────────────────────┤
│ - _audioService: IAudioService                              │
│ - _eventBus: IEventBus                                      │
│ - _currentMusicZone: string?                                │
│ - _currentAmbientHandle: AudioHandle                        │
├────────────────────────────────────────────────────────────┤
│ + AudioZoneSystem(audioService, eventBus)                  │
│ + Priority: int                                             │
│ # OnInitialized(): void                                     │
│ - OnMapTransition(event): void                              │
│ - HandleAudioZoneTransition(zone): void                     │
└────────────────────────────────────────────────────────────┘
                          │
                          │ queries for
                          ▼
┌────────────────────────────────────────────────────────────┐
│                 AudioZone                                   │
│                 (ECS Component)                             │
├────────────────────────────────────────────────────────────┤
│ + MusicTrackId: string                                      │
│ + AmbientLoopId: string?                                    │
│ + AmbientVolume: float                                      │
│ + ContinueMusicAcrossMaps: bool                             │
└────────────────────────────────────────────────────────────┘
                          △
                          │
                          │ attached to
┌─────────────────────────▼─────────────────────────────────┐
│                   MapInfo                                   │
│                   (ECS Component)                           │
├────────────────────────────────────────────────────────────┤
│ + MapId: GameMapId                                          │
│ + MapName: string                                           │
│ + Width: int                                                │
│ + Height: int                                               │
└────────────────────────────────────────────────────────────┘
```

---

## 8. Integration with Existing Systems

### 8.1 Service Registration (DI Container)

```csharp
// In ServiceCollectionExtensions.cs or new AudioServicesExtensions.cs
public static class AudioServicesExtensions
{
    public static IServiceCollection AddAudioServices(this IServiceCollection services)
    {
        // Core audio service (singleton)
        services.AddSingleton<IAudioService, AudioService>();

        // Pokemon cry management
        services.AddSingleton<IPokemonCryService, PokemonCryService>();

        // Battle audio orchestration (transient - created per battle)
        services.AddTransient<BattleAudioOrchestrator>();

        // Audio source factory
        services.AddSingleton<IAudioSourceFactory, AudioSourceFactory>();

        // Optional: Audio configuration
        services.Configure<AudioConfiguration>(config =>
        {
            config.MaxConcurrentSounds = 32;
            config.PokemonCryPoolSize = 200;
            config.EnableSpatialAudio = true;
            config.MasterVolume = 0.8f;
        });

        return services;
    }
}
```

### 8.2 System Registration

```csharp
// In MonoBallFrameworkGame.cs Initialize() or SystemManager setup
_systemManager.RegisterSystem(new AudioSystem(_audioService, _eventBus));
_systemManager.RegisterSystem(new AudioZoneSystem(_audioService, _eventBus));
```

### 8.3 Event Integration Examples

```csharp
// Example: Play footstep sound when player moves
public class MovementSystem : SystemBase, IUpdateSystem
{
    private readonly IEventBus _eventBus;

    private void OnMovementCompleted(Entity entity)
    {
        // Publish sound effect request
        _eventBus.PublishPooled<SoundEffectRequestedEvent>(evt =>
        {
            evt.SoundId = "footstep_grass";
            evt.Config = new AudioSourceConfig
            {
                Volume = 0.5f,
                Category = AudioCategory.SoundEffect,
                Pitch = 1.0f + Random.Shared.NextSingle() * 0.2f - 0.1f // Vary pitch
            };
        });
    }
}

// Example: Play cry when Pokemon appears
public class PokemonEncounterSystem : SystemBase
{
    private readonly IEventBus _eventBus;

    private void OnWildPokemonEncounter(int pokemonId)
    {
        _eventBus.PublishPooled<PokemonCryRequestedEvent>(evt =>
        {
            evt.PokemonId = pokemonId;
            evt.Config = new AudioSourceConfig
            {
                Volume = 1.0f,
                Category = AudioCategory.Pokemon
            };
        });
    }
}

// Example: Change music when entering new map
// This is handled automatically by AudioZoneSystem via MapTransitionEvent
```

---

## 9. File Structure Recommendations

```
MonoBallFramework.Game/
├── Engine/
│   └── Audio/
│       ├── Components/
│       │   ├── AudioListener.cs
│       │   ├── AudioSource.cs
│       │   ├── AmbientAudioEmitter.cs
│       │   └── PokemonAudio.cs
│       ├── Events/
│       │   ├── MusicChangeRequestedEvent.cs
│       │   ├── SoundEffectRequestedEvent.cs
│       │   ├── PokemonCryRequestedEvent.cs
│       │   └── AudioVolumeChangedEvent.cs
│       ├── Services/
│       │   ├── IAudioService.cs
│       │   ├── AudioService.cs
│       │   ├── IPokemonCryService.cs
│       │   ├── PokemonCryService.cs
│       │   ├── IAudioSourceFactory.cs
│       │   └── AudioSourceFactory.cs
│       ├── Systems/
│       │   ├── AudioSystem.cs
│       │   └── AudioZoneSystem.cs
│       ├── Core/
│       │   ├── AudioHandle.cs
│       │   ├── AudioCategory.cs
│       │   ├── AudioSourceConfig.cs
│       │   ├── AudioChannel.cs
│       │   └── AudioConfiguration.cs
│       ├── Pooling/
│       │   ├── AudioInstancePool.cs
│       │   └── ActiveSound.cs
│       ├── Battle/
│       │   ├── BattleAudioOrchestrator.cs
│       │   ├── BattleAudioState.cs
│       │   └── IBattleAudioStateHandler.cs
│       ├── Modes/
│       │   ├── AudioMode.cs
│       │   ├── IAudioModeState.cs
│       │   ├── BattleAudioMode.cs
│       │   ├── MenuAudioMode.cs
│       │   └── CutsceneAudioMode.cs
│       └── Music/
│           ├── MusicPlayer.cs
│           ├── MusicTrack.cs
│           └── MusicCrossfader.cs
│
├── Infrastructure/
│   └── ServiceRegistration/
│       └── AudioServicesExtensions.cs
│
└── Assets/
    ├── Music/
    │   ├── bgm_route_001.ogg
    │   ├── bgm_battle_wild.ogg
    │   ├── bgm_battle_trainer.ogg
    │   └── ...
    ├── Sounds/
    │   ├── SFX/
    │   │   ├── menu_select.wav
    │   │   ├── menu_back.wav
    │   │   ├── footstep_grass.wav
    │   │   └── ...
    │   ├── Cries/
    │   │   ├── pokemon_001_bulbasaur.wav
    │   │   ├── pokemon_004_charmander.wav
    │   │   ├── pokemon_007_squirtle.wav
    │   │   └── ... (800+ files)
    │   └── Ambient/
    │       ├── forest_ambience.ogg
    │       ├── ocean_waves.ogg
    │       └── ...
    └── Audio/
        └── audio_metadata.json (cry mappings, volume defaults, etc.)
```

---

## 10. Performance Considerations

### 10.1 Memory Optimization Strategies

1. **Lazy Loading:** Only load sounds when needed
2. **Pooling:** Reuse SoundEffectInstance objects
3. **Streaming:** Use MediaPlayer for large music files
4. **Cache Eviction:** Unload unused cries after 5 minutes
5. **Compression:** Use OGG for music, WAV for SFX

### 10.2 CPU Optimization

1. **Event-Driven:** Only update audio when events occur
2. **Spatial Audio Culling:** Don't update sounds outside audible range
3. **Batch Processing:** Update all spatial audio in single pass
4. **Dirty Flags:** Only recalculate when positions change

### 10.3 Recommended Limits

```csharp
public class AudioConfiguration
{
    public int MaxConcurrentSounds { get; set; } = 32;
    public int MaxPokemonCriesLoaded { get; set; } = 200;
    public int SoundInstancePoolSize { get; set; } = 5; // per sound
    public float SpatialAudioMaxDistance { get; set; } = 160f; // 10 tiles
    public TimeSpan CryEvictionTime { get; set; } = TimeSpan.FromMinutes(5);
}
```

---

## 11. Testing Strategy

### 11.1 Unit Tests

```csharp
[TestClass]
public class AudioServiceTests
{
    [TestMethod]
    public void PlayMusic_ShouldSetCurrentTrack()
    {
        var audioService = new AudioService(mockContent, mockPokemonCry);
        audioService.PlayMusic("test_track");

        Assert.AreEqual("test_track", audioService.CurrentMusicTrack);
    }

    [TestMethod]
    public void SetCategoryVolume_ShouldAffectAllSoundsInCategory()
    {
        var audioService = new AudioService(mockContent, mockPokemonCry);
        audioService.SetCategoryVolume(AudioCategory.SoundEffect, 0.5f);

        Assert.AreEqual(0.5f, audioService.GetCategoryVolume(AudioCategory.SoundEffect));
    }

    [TestMethod]
    public void AudioHandle_ShouldBeUnique()
    {
        var audioService = new AudioService(mockContent, mockPokemonCry);
        var handle1 = audioService.PlaySound("sound1");
        var handle2 = audioService.PlaySound("sound2");

        Assert.AreNotEqual(handle1.Id, handle2.Id);
    }
}
```

### 11.2 Integration Tests

```csharp
[TestClass]
public class AudioSystemIntegrationTests
{
    [TestMethod]
    public void MapTransition_ShouldChangeMusic()
    {
        // Arrange
        var world = CreateTestWorld();
        var audioService = CreateTestAudioService();
        var eventBus = CreateTestEventBus();
        var audioZoneSystem = new AudioZoneSystem(audioService, eventBus);

        // Create maps with different music
        CreateMapWithAudioZone(world, "route_001", "music_route_001");
        CreateMapWithAudioZone(world, "pallet_town", "music_pallet_town");

        // Act
        eventBus.Publish(new MapTransitionEvent
        {
            ToMapId = new GameMapId("pallet_town")
        });

        // Assert
        Assert.AreEqual("music_pallet_town", audioService.CurrentMusicTrack);
    }
}
```

---

## 12. Future Enhancements

### 12.1 Advanced Features (Post-MVP)

1. **Dynamic Music System:** Layer-based music that adds/removes layers based on game state
2. **Audio Ducking:** Automatically lower music volume when dialogue plays
3. **Reverb Zones:** Environment-specific audio effects
4. **Voice Acting:** Support for NPC voice lines
5. **Audio Scripting:** Lua/C# scripts for complex audio sequences
6. **Music Synchronization:** Beat-matched transitions between tracks

### 12.2 Performance Enhancements

1. **Audio Streaming for Cries:** Stream large cry files instead of loading all
2. **Compressed Audio Format:** Use Opus or AAC for better compression
3. **Audio Asset Bundles:** Group related sounds for faster loading
4. **Multi-threaded Audio Loading:** Load sounds on background threads

---

## 13. Summary & Recommendations

### 13.1 Implementation Priority

**Phase 1 - Core Foundation (Week 1-2):**
1. ✅ IAudioService interface and AudioService implementation
2. ✅ Basic audio components (AudioSource, AudioListener)
3. ✅ AudioSystem for event-driven playback
4. ✅ Basic event types (MusicChangeRequested, SoundEffectRequested)
5. ✅ Service registration and DI integration

**Phase 2 - Pokemon Integration (Week 3):**
1. ✅ IPokemonCryService and implementation
2. ✅ PokemonAudio component
3. ✅ Pokemon cry lazy loading
4. ✅ PokemonCryRequestedEvent

**Phase 3 - Battle Audio (Week 4):**
1. ✅ BattleAudioOrchestrator
2. ✅ Battle audio state machine
3. ✅ Victory/defeat jingles
4. ✅ Battle event integration

**Phase 4 - World Audio (Week 5):**
1. ✅ AudioZone component and system
2. ✅ Map transition audio handling
3. ✅ Ambient audio emitters
4. ✅ Spatial audio calculations

**Phase 5 - Polish & Optimization (Week 6):**
1. ✅ Audio pooling optimization
2. ✅ Memory profiling and tuning
3. ✅ Configuration system
4. ✅ Unit and integration tests

### 13.2 Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Service Pattern** | Service Pattern (not Singleton) | Consistent with codebase, better DI support |
| **BGM Playback** | MediaPlayer (streaming) | Large files, only 1 track active |
| **SFX Playback** | SoundEffectInstance (pooled) | Many concurrent, needs pooling |
| **Pokemon Cries** | Lazy load + cache | 800+ sounds, can't load all |
| **Event System** | IEventBus (pooled events) | Existing infrastructure, zero-alloc |
| **ECS Integration** | Components + Systems | Clean separation, ECS best practices |
| **Spatial Audio** | 2D distance-based | Matches top-down perspective |

### 13.3 Code Quality Metrics

- **SOLID Compliance:** ✅ All interfaces follow Single Responsibility
- **DRY Principle:** ✅ Shared pooling, event patterns
- **Testability:** ✅ All services mockable, systems testable
- **Performance:** ✅ Object pooling, lazy loading, caching
- **Maintainability:** ✅ Clear separation of concerns, documented

---

## Conclusion

This audio system architecture provides a robust, scalable foundation for PokeSharp's audio needs while maintaining consistency with the existing MonoBallFramework architecture. The design prioritizes:

1. **Integration:** Seamless fit with existing ECS, Event Bus, and Service patterns
2. **Performance:** Memory-efficient pooling and lazy loading
3. **Flexibility:** Event-driven design allows easy extension
4. **Pokemon-Specific:** Dedicated systems for cries, battles, and zones
5. **Maintainability:** Clean architecture with clear responsibilities

The proposed system follows industry best practices and established patterns from the existing codebase, ensuring it will be maintainable and extensible as the game grows.

---

**Next Steps:**
1. Review architecture with development team
2. Create implementation tasks from Phase 1 priorities
3. Setup audio asset pipeline
4. Begin TDD implementation of IAudioService

**Files to Create:** 25-30 new files
**Estimated LOC:** ~3,500-4,000 lines
**Estimated Implementation Time:** 6 weeks (1 developer)
