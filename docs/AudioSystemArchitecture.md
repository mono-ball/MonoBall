# Audio System Architecture

## Overview

The audio system is a comprehensive, production-ready implementation designed for the MonoGame Pokemon engine. It provides robust audio management with support for sound effects, background music, Pokemon cries, and battle audio orchestration.

## Architecture Layers

### 1. Core Services (`Engine/Audio/Services/`)

#### IAudioService / AudioService
The main entry point for all audio operations.

**Key Features:**
- Master volume control with separate channels (SFX, Music)
- Sound effect playback with volume/pitch/pan control
- Looping sound management
- Music playback with fade-in/fade-out
- Asset preloading and caching
- Thread-safe operations

**Usage Example:**
```csharp
_audioService.PlaySound("Audio/SFX/Select", volume: 0.8f);
_audioService.PlayMusic("Audio/Music/Route1", loop: true, fadeDuration: 2.0f);
```

#### ISoundEffectPool / SoundEffectPool
Pooled instance manager for efficient memory usage.

**Key Features:**
- Configurable concurrent instance limit (default: 32)
- Automatic instance recycling
- Oldest non-looping instance stealing when pool is full
- Zero-allocation updates for stopped sounds

**Benefits:**
- Reduces GC pressure
- Prevents memory fragmentation
- Manages MonoGame's 256 instance limit automatically

#### IMusicPlayer / MusicPlayer
Background music player with advanced features.

**Key Features:**
- Smooth crossfading between tracks
- Fade-in/fade-out support
- Track caching and preloading
- Pause/resume functionality
- Automatic looping

**Usage Example:**
```csharp
_musicPlayer.Crossfade("Audio/Music/Battle", crossfadeDuration: 1.5f);
```

#### IPokemonCryManager / PokemonCryManager
Manages 800+ Pokemon cries efficiently.

**Key Features:**
- Species ID and name-based lookup
- Form-specific cry variants
- Automatic form fallback
- Selective preloading for route/battle optimization
- Memory-efficient caching

**Usage Example:**
```csharp
_pokemonCryManager.PlayCry(25); // Pikachu by ID
_pokemonCryManager.PlayCry("Pikachu"); // By name
_pokemonCryManager.PlayCryWithForm(25, 1, pitch: 0.1f); // Specific form
```

#### IBattleAudioManager / BattleAudioManager
Battle audio orchestration coordinator.

**Key Features:**
- Battle-type specific music selection
- Move sound effects with type fallbacks
- Pokemon cry playback with side-specific pitch
- UI sound feedback
- Status condition sounds
- Low health warning loop
- Music restoration after battle

**Usage Example:**
```csharp
_battleAudioManager.StartBattleMusic(BattleType.GymLeader);
_battleAudioManager.PlayMoveSound("Thunderbolt", "Electric");
_battleAudioManager.PlayBattleCry(25, isPlayerPokemon: true);
```

#### IAudioManager / AudioManager
Central coordinator for all audio subsystems.

**Purpose:**
- Provides unified access to all audio managers
- Coordinates updates across subsystems
- Manages 3D audio listener position
- Centralized disposal

### 2. ECS Components (`Ecs/Components/Audio/`)

#### SoundEffectComponent
Attaches sound effects to entities.

**Use Cases:**
- Footstep sounds on characters
- Interaction sounds on NPCs
- Attack sounds on Pokemon
- Ambient object sounds (fountains, machines)

**Example:**
```csharp
entity.Add(new SoundEffectComponent("Audio/SFX/Footstep", volume: 0.7f, isLooping: false));
```

#### AmbientSoundComponent
Environmental sounds for zones and areas.

**Use Cases:**
- Cave ambience
- Water flowing sounds
- Weather effects (rain, wind)
- Location-specific atmosphere

**Features:**
- Zone-based activation
- Distance-based volume attenuation
- Automatic fade-in/fade-out
- Global or localized sounds

**Example:**
```csharp
entity.Add(new AmbientSoundComponent(
    "Audio/Ambient/Cave",
    volume: 0.8f,
    maxDistance: 15f
));
```

#### AudioEmitterComponent
Positional 3D audio sources.

**Use Cases:**
- Waterfall sounds that pan left/right
- Pokemon cries from off-screen
- Battle sounds with directional audio
- Interactive environment sounds

**Features:**
- Distance-based volume rolloff
- Horizontal panning simulation
- Configurable reference/max distance
- Adjustable rolloff factor

**Example:**
```csharp
entity.Add(new AudioEmitterComponent(
    position: new Vector2(100, 50),
    soundName: "Audio/SFX/Waterfall",
    maxDistance: 20f,
    volume: 1.0f
));
```

#### MusicZoneComponent
Automatic music changes based on player position.

**Use Cases:**
- Route-specific music
- Building interior music
- Battle area music
- Special event zones

**Features:**
- Priority-based zone overlapping
- Smooth crossfading
- Rectangular zone bounds
- Entry/exit detection

**Example:**
```csharp
entity.Add(new MusicZoneComponent(
    musicName: "Audio/Music/PalletTown",
    zoneBounds: new Rectangle(0, 0, 50, 50),
    priority: 1
));
```

### 3. ECS Systems (`Engine/Audio/Systems/`)

#### AudioEventSystem
Bridges the event bus with audio services.

**Subscribed Events:**
- `PlaySoundEvent`
- `PlayMusicEvent`
- `StopMusicEvent`
- `FadeMusicEvent`
- `PlayPokemonCryEvent`
- `PlayMoveSoundEvent`
- `PauseMusicEvent`
- `ResumeMusicEvent`
- `StopAllSoundsEvent`

**Usage:**
```csharp
_eventBus.PublishPooled<PlaySoundEvent>(evt => {
    evt.SoundName = "Audio/SFX/Select";
    evt.Volume = 0.8f;
});
```

#### AudioEmitterSystem
Updates positional audio based on listener position.

**Responsibilities:**
- Calculate distance-based volume
- Apply horizontal panning
- Update sound instances
- Handle looping emitter sounds

**Integration:**
```csharp
audioEmitterSystem.SetListenerPosition(playerPosition);
audioEmitterSystem.Update(deltaTime);
```

#### MusicZoneSystem
Manages music zone transitions.

**Responsibilities:**
- Detect zone entry/exit
- Resolve overlapping zones by priority
- Trigger music changes with crossfading
- Track active zone state

#### AmbientSoundSystem
Manages ambient sound playback and volume.

**Responsibilities:**
- Zone-based activation
- Distance-based volume attenuation
- Smooth volume fading
- Looping instance management

### 4. Event System (`Engine/Audio/Events/`)

All audio events inherit from `NotificationEventBase` for pooling support.

**Available Events:**
- `PlaySoundEvent` - Play a one-shot sound effect
- `PlayMusicEvent` - Start background music
- `StopMusicEvent` - Stop background music
- `FadeMusicEvent` - Crossfade to new music
- `PlayPokemonCryEvent` - Play Pokemon vocalization
- `PlayMoveSoundEvent` - Play battle move sound
- `PauseMusicEvent` - Pause music
- `ResumeMusicEvent` - Resume music
- `StopAllSoundsEvent` - Stop all sound effects

## Integration Guide

### 1. Dependency Injection Setup

```csharp
// Register audio services
services.AddSingleton<ISoundEffectPool>(sp =>
    new SoundEffectPool(maxConcurrentInstances: 32));

services.AddSingleton<IMusicPlayer>(sp =>
    new MusicPlayer(sp.GetRequiredService<ContentManager>()));

services.AddSingleton<IPokemonCryManager>(sp =>
    new PokemonCryManager(
        sp.GetRequiredService<ContentManager>(),
        sp.GetRequiredService<ISoundEffectPool>()));

services.AddSingleton<IBattleAudioManager>(sp =>
    new BattleAudioManager(
        sp.GetRequiredService<IAudioService>(),
        sp.GetRequiredService<IPokemonCryManager>()));

services.AddSingleton<IAudioService>(sp =>
    new AudioService(
        sp.GetRequiredService<ContentManager>(),
        sp.GetRequiredService<ISoundEffectPool>(),
        sp.GetRequiredService<IMusicPlayer>()));

services.AddSingleton<IAudioManager>(sp =>
    new AudioManager(
        sp.GetRequiredService<ISoundEffectPool>(),
        sp.GetRequiredService<IMusicPlayer>(),
        sp.GetRequiredService<IPokemonCryManager>(),
        sp.GetRequiredService<IBattleAudioManager>()));

// Register audio systems
services.AddSingleton<AudioEventSystem>();
services.AddSingleton<AudioEmitterSystem>();
services.AddSingleton<MusicZoneSystem>();
services.AddSingleton<AmbientSoundSystem>();
```

### 2. Game Initialization

```csharp
public class Game1 : Game
{
    private IAudioService _audioService;
    private AudioEventSystem _audioEventSystem;

    protected override void Initialize()
    {
        base.Initialize();

        _audioService = Services.GetService<IAudioService>();
        _audioService.Initialize();

        _audioEventSystem = Services.GetService<AudioEventSystem>();
    }

    protected override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        _audioService.Update(deltaTime);

        base.Update(gameTime);
    }
}
```

### 3. Using Events for Decoupled Audio

```csharp
// From any system or component
_eventBus.PublishPooled<PlaySoundEvent>(evt => {
    evt.SoundName = "Audio/SFX/MenuSelect";
    evt.Volume = 0.8f;
});

_eventBus.PublishPooled<PlayMusicEvent>(evt => {
    evt.MusicName = "Audio/Music/Battle_Wild";
    evt.Loop = true;
    evt.FadeDuration = 1.0f;
});

_eventBus.PublishPooled<PlayPokemonCryEvent>(evt => {
    evt.SpeciesId = 25; // Pikachu
    evt.Volume = 0.9f;
});
```

### 4. ECS Integration

```csharp
// Creating audio entities
var ambientSound = world.Create(
    new AmbientSoundComponent("Audio/Ambient/Forest", volume: 0.6f, maxDistance: 20f),
    new MapWorldPosition { X = 50, Y = 50 }
);

var musicZone = world.Create(
    new MusicZoneComponent(
        "Audio/Music/ViridianForest",
        new Rectangle(0, 0, 100, 100),
        priority: 1
    )
);

// Update systems
ambientSoundSystem.SetListenerPosition(playerPosition);
ambientSoundSystem.Update(deltaTime);

musicZoneSystem.SetPlayerPosition(playerPosition);
musicZoneSystem.Update(deltaTime);
```

## Asset Organization

```
Content/Audio/
├── SFX/                    # Sound effects
│   ├── Menu/
│   │   ├── Select.wav
│   │   ├── Back.wav
│   │   └── Invalid.wav
│   ├── Movement/
│   │   └── Footstep.wav
│   └── Environment/
│       └── DoorOpen.wav
├── Music/                  # Background music
│   ├── Route1.ogg
│   ├── PalletTown.ogg
│   ├── Battle_Wild.ogg
│   └── Battle_Trainer.ogg
├── Cries/                  # Pokemon cries
│   ├── 001.wav             # Bulbasaur
│   ├── 025.wav             # Pikachu
│   └── 025_01.wav          # Pikachu (form variant)
├── Ambient/                # Ambient sounds
│   ├── Cave.ogg
│   ├── Forest.ogg
│   └── Water.ogg
└── Battle/                 # Battle-specific sounds
    ├── Encounter_Wild.wav
    ├── Low_Health_Warning.wav
    └── Moves/
        ├── Thunderbolt.wav
        └── Tackle.wav
```

## Performance Considerations

### Memory Management
- **Sound Effect Pool**: Limits concurrent instances to prevent memory exhaustion
- **Cry Caching**: Selective preloading for routes/battles reduces load times
- **Music Streaming**: Uses MonoGame's Song class for streamed playback
- **Asset Disposal**: Proper cleanup prevents memory leaks

### CPU Optimization
- **Event Pooling**: All audio events use object pooling to reduce GC pressure
- **Batch Updates**: Systems process all entities in single queries
- **Distance Culling**: Sounds beyond max distance don't play
- **Instance Recycling**: Stopped sounds automatically return to pool

### Audio Quality
- **Sound Effects**: Use WAV format for low-latency playback
- **Music**: Use OGG format for compressed streaming
- **Sample Rates**: 44.1kHz recommended for compatibility
- **Bit Depth**: 16-bit for effects, variable for music

## Best Practices

### 1. Volume Management
```csharp
// Use layered volume control
_audioService.MasterVolume = 0.8f;     // Overall game volume
_audioService.SoundEffectVolume = 1.0f; // SFX at 100%
_audioService.MusicVolume = 0.7f;       // Music at 70%

// Individual sounds can also have volume
_audioService.PlaySound("click", volume: 0.5f);
```

### 2. Preloading for Performance
```csharp
// Preload frequently used sounds at startup
_audioService.PreloadAssets(
    "Audio/SFX/Select",
    "Audio/SFX/Back",
    "Audio/SFX/Invalid"
);

// Preload route Pokemon cries
_pokemonCryManager.PreloadCries(16, 17, 18, 19, 20); // Route 1 Pokemon
```

### 3. Battle Audio Flow
```csharp
// Battle start
_battleAudioManager.StartBattleMusic(BattleType.Wild);
_battleAudioManager.PlayEncounterSound(EncounterType.Wild);

// Battle actions
_battleAudioManager.PlayBattleCry(25, isPlayerPokemon: true);
_battleAudioManager.PlayMoveSound("Thunderbolt", "Electric");
_battleAudioManager.PlayUISound(BattleUIAction.Select);

// Low health
if (playerPokemon.HP < playerPokemon.MaxHP * 0.25f)
    _battleAudioManager.PlayLowHealthWarning();

// Battle end
_battleAudioManager.StopBattleMusic(fadeOutDuration: 1.0f);
```

### 4. Error Handling
All audio operations return safely if:
- Audio files are missing (logs error, continues)
- Pool is full (oldest non-looping sound is recycled)
- Invalid parameters (clamped to valid ranges)

### 5. Thread Safety
- All public APIs are thread-safe
- Internal state uses proper locking
- Event bus handles concurrent publishes

## Testing Recommendations

### Unit Tests
- Test volume clamping (0.0 to 1.0)
- Test pool instance limits
- Test cry ID/name mapping
- Test fade calculations

### Integration Tests
- Test event bus → audio service flow
- Test ECS system updates
- Test music zone priority resolution
- Test distance-based volume calculations

### Performance Tests
- Stress test with 32+ concurrent sounds
- Test 800+ cry loading times
- Measure GC allocations per frame
- Profile distance calculation performance

## Future Enhancements

### Potential Features
- 3D audio with full spatial positioning
- Audio ducking (reduce music during dialogue)
- Dynamic music layering (add drums during battle)
- Audio compression for mobile platforms
- FMOD or Wwise integration for advanced features
- Audio reverb zones (caves, buildings)
- Doppler effect for fast-moving entities

### Extensibility Points
- Custom `IBattleAudioManager` implementations
- Additional audio event types
- Custom volume curve functions
- Platform-specific audio backends

## Troubleshooting

### No Sound Playing
1. Check `IAudioService.IsInitialized`
2. Verify `MasterVolume` > 0
3. Check asset paths in Content.mgcb
4. Ensure `Update()` is being called

### Crackling/Popping
1. Reduce concurrent instance count
2. Check sample rates match
3. Verify buffer sizes
4. Check for CPU spikes

### Memory Leaks
1. Ensure `Dispose()` is called on services
2. Check for unreturned looping instances
3. Verify event subscriptions are disposed
4. Clear caches when changing maps

## Summary

This audio system provides a robust, performant, and maintainable solution for Pokemon game audio. It follows clean architecture principles, integrates seamlessly with the ECS pattern, and supports the event bus for decoupled communication. The pooling and caching strategies ensure excellent performance even with hundreds of audio assets.
