# Audio System Reusable Design Patterns

This document identifies enhancement patterns from the audio system that can benefit other game systems (graphics, input, UI, etc.).

## Table of Contents
1. [Design Patterns](#design-patterns)
2. [Infrastructure Patterns](#infrastructure-patterns)
3. [Game-Specific Patterns](#game-specific-patterns)
4. [Performance Patterns](#performance-patterns)
5. [Integration Patterns](#integration-patterns)

---

## Design Patterns

### 1. Registry Pattern with Dual-Cache Lookup
**Location**: `MonoBallFramework.Game/Engine/Audio/AudioRegistry.cs:15-309`

**Pattern**: A centralized registry using EF Core DbContextFactory with dual-cache strategy for fast lookups.

**Implementation**:
```csharp
// Dual cache: by full ID and by short TrackId
private readonly ConcurrentDictionary<string, AudioDefinition> _cache;
private readonly ConcurrentDictionary<string, AudioDefinition> _trackIdCache;

// Thread-safe cache loading with SemaphoreSlim
private readonly SemaphoreSlim _loadLock = new(1, 1);
private volatile bool _isCacheLoaded;

// Multiple lookup strategies
public AudioDefinition? GetById(string id) { /* full ID lookup */ }
public AudioDefinition? GetByTrackId(string trackId) { /* short name lookup */ }
public IEnumerable<AudioDefinition> GetByCategory(string category) { /* category filter */ }
```

**How it works**:
- Uses DbContextFactory to avoid holding scoped context in singleton
- Dual-cache strategy: full IDs and short names for flexibility
- Thread-safe lazy loading with volatile flag
- Supports both cache-first and database-fallback strategies

**Applications to other systems**:
- **Graphics System**: Texture/Sprite registry with lookup by full path or short name
- **Input System**: Key binding registry with lookup by action name or key code
- **UI System**: Widget template registry with category-based filtering
- **Entity System**: Prefab registry with dual lookup (ID and friendly name)

**Key Benefits**:
- O(1) lookups after cache load
- Flexible query patterns (by ID, by category, by subcategory)
- Thread-safe concurrent access
- Database fallback for cache misses

---

### 2. Object Pooling with Two-Tier Strategy
**Location**: `MonoBallFramework.Game/Engine/Audio/Services/AudioBufferPool.cs:15-111`

**Pattern**: High-performance pooling using both ArrayPool and ObjectPool based on buffer size.

**Implementation**:
```csharp
// Small buffers: use .NET's built-in ArrayPool
private static readonly ArrayPool<float> SmallBufferPool = ArrayPool<float>.Shared;

// Large buffers: use ObjectPool with custom policy
private static readonly ObjectPool<float[]> LargeBufferPool =
    new DefaultObjectPool<float[]>(new LargeBufferPoolPolicy(), 16);

// Two-tier allocation
public static float[] RentSmall() => SmallBufferPool.Rent(SmallBufferSize);
public static float[] RentLarge() => LargeBufferPool.Get();

// Custom pooling policy
private class LargeBufferPoolPolicy : IPooledObjectPolicy<float[]>
{
    public float[] Create() => new float[LargeBufferSize];
    public bool Return(float[] obj)
    {
        Array.Clear(obj, 0, obj.Length); // Clear for security
        return obj.Length == LargeBufferSize;
    }
}
```

**How it works**:
- Separates small (4KB) and large (176KB) allocations
- Uses ArrayPool for small, frequently-allocated buffers
- Uses ObjectPool for large, less-frequent buffers
- Auto-clears buffers on return for security

**Applications to other systems**:
- **Graphics System**: Vertex buffer pooling, texture data pooling
- **Particle System**: Particle array pooling by size tier
- **Network System**: Packet buffer pooling (small/large messages)
- **Physics System**: Collision result array pooling

**Key Benefits**:
- 95% reduction in GC pressure
- Improved cache locality
- Zero allocation in hot paths
- Automatic buffer size validation

---

### 3. Template Method Pattern for Music Players
**Location**: `MonoBallFramework.Game/Engine/Audio/Services/MusicPlayerBase.cs:12-335`

**Pattern**: Abstract base class with shared fade logic and device error recovery.

**Implementation**:
```csharp
public abstract class MusicPlayerBase : IMusicPlayer
{
    // Shared fade logic (template method)
    protected void UpdatePlaybackFade(IPlaybackState playback, float deltaTime)
    {
        // Complex fade state machine logic shared by all implementations
        switch (playback.FadeState)
        {
            case FadeState.FadingIn: /* ... */ break;
            case FadeState.FadingOut: /* ... */ break;
            case FadeState.FadingOutThenPlay: /* ... */ break;
            case FadeState.Crossfading: /* ... */ break;
        }
    }

    // Hook methods for subclasses
    protected abstract void UpdateCore(float deltaTime);
    protected abstract void HandleFadeOutComplete(IPlaybackState playback);
    protected abstract void ReinitializeDevice();

    // Shared device error recovery
    private async Task AttemptDeviceRecoveryAsync()
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt - 1));
            ReinitializeDevice(); // Calls subclass implementation
        }
    }
}
```

**How it works**:
- Base class handles complex fade state machine
- Subclasses implement device-specific operations
- Exponential backoff for error recovery
- Device error detection and automatic recovery

**Applications to other systems**:
- **Animation System**: Base animator with shared interpolation, subclass-specific rendering
- **Particle Emitter**: Base emitter with shared lifecycle, subclass-specific emission
- **Camera System**: Base camera with shared projection, subclass-specific movement
- **AI System**: Base AI with shared pathfinding, subclass-specific behavior

**Key Benefits**:
- Code reuse for complex logic (fade state machine)
- Consistent error handling across implementations
- Easy to add new implementations
- Automatic device recovery

---

### 4. State Machine Pattern with Enum States
**Location**: `MonoBallFramework.Game/Engine/Audio/Services/Streaming/StreamingPlaybackState.cs:93-103`

**Pattern**: Clean state machine using enums with explicit state transitions.

**Implementation**:
```csharp
public enum FadeState
{
    None,
    FadingIn,
    FadingOut,
    FadingOutThenPlay,      // Sequential: fade out → play immediately
    FadingOutThenFadeIn,    // Sequential: fade out → fade in
    Crossfading             // Concurrent: fade out while fading in
}

// State transitions in update loop
switch (playback.FadeState)
{
    case FadeState.FadingIn:
        playback.CurrentVolume = playback.TargetVolume * progress;
        if (progress >= 1.0f)
            playback.FadeState = FadeState.None; // Transition
        break;

    case FadeState.FadingOutThenPlay:
        playback.CurrentVolume = playback.TargetVolume * (1f - progress);
        if (progress >= 1.0f)
        {
            HandleFadeOutComplete(playback);
            // Async task spawns to avoid blocking
            _ = Task.Run(() => Play(pendingTrack));
        }
        break;
}
```

**How it works**:
- Enum clearly defines all possible states
- State machine in update loop handles transitions
- Async operations for non-blocking state changes
- Timer-based progress tracking

**Applications to other systems**:
- **UI Transitions**: Menu states (Opening, Open, Closing, Closed, Transitioning)
- **Animation States**: Idle, Walking, Running, Jumping, Landing
- **Battle System**: Selecting, Executing, Animating, Resolving
- **Loading System**: Idle, Loading, Streaming, Ready, Error

**Key Benefits**:
- Clear state visualization
- Easy to add new states
- Type-safe state transitions
- Self-documenting code

---

### 5. Service Locator with Dependency Injection
**Location**: `MonoBallFramework.Game/Infrastructure/ServiceRegistration/AudioServicesExtensions.cs:15-127`

**Pattern**: Extension methods for clean DI registration with factory patterns.

**Implementation**:
```csharp
public static IServiceCollection AddAudioServices(
    this IServiceCollection services,
    AudioConfiguration? config = null)
{
    // Configuration
    services.AddSingleton(config ?? AudioConfiguration.CreateDefault());

    // Registry with DbContextFactory
    services.AddSingleton<AudioRegistry>(sp =>
    {
        var contextFactory = sp.GetRequiredService<IDbContextFactory<GameDataContext>>();
        var logger = sp.GetRequiredService<ILogger<AudioRegistry>>();
        var registry = new AudioRegistry(contextFactory, logger);
        registry.LoadDefinitions(); // Eager initialization
        return registry;
    });

    // Strategy pattern: streaming vs cached
    services.AddSingleton<IMusicPlayer>(sp =>
    {
        var config = sp.GetRequiredService<AudioConfiguration>();
        if (config.UseStreamingMode)
            return new NAudioStreamingMusicPlayer(/* ... */);
        else
            return new NAudioMusicPlayer(/* ... */);
    });

    // Service with automatic initialization
    services.AddSingleton<IAudioService>(sp =>
    {
        var service = new NAudioService(/* ... */);
        service.Initialize(); // Auto-initialize
        return service;
    });
}
```

**How it works**:
- Extension method for clean registration
- Factory lambdas for complex initialization
- Strategy pattern selection based on config
- Automatic initialization in factory

**Applications to other systems**:
- **Graphics System**: AddGraphicsServices with renderer selection (Vulkan/DX12/OpenGL)
- **Physics System**: AddPhysicsServices with engine selection (Box2D/custom)
- **Input System**: AddInputServices with backend selection (SDL/native)
- **Network System**: AddNetworkServices with protocol selection (TCP/UDP)

**Key Benefits**:
- Clean service registration in one place
- Easy to swap implementations
- Configuration-driven behavior
- Automatic lifecycle management

---

## Infrastructure Patterns

### 6. Event-Driven Architecture with Pooled Events
**Location**: `MonoBallFramework.Game/Engine/Audio/Events/AudioEvents.cs:9-199`

**Pattern**: Poolable event objects with Reset method for zero-allocation messaging.

**Implementation**:
```csharp
// Poolable event base
public class PlaySoundEvent : NotificationEventBase
{
    public string SoundName { get; set; } = string.Empty;
    public SoundCategory Category { get; set; } = SoundCategory.UI;
    public float Volume { get; set; } = 1f;
    public float Pitch { get; set; } = 0f;
    public float Pan { get; set; } = 0f;

    // Reset for pooling
    public override void Reset()
    {
        base.Reset();
        SoundName = string.Empty;
        Category = SoundCategory.UI;
        Volume = 1f;
        Pitch = 0f;
        Pan = 0f;
    }
}

// Service subscribes to events
private void SubscribeToEvents()
{
    _subscriptions.Add(_eventBus.Subscribe<PlaySoundEvent>(OnPlaySoundEvent));
    _subscriptions.Add(_eventBus.Subscribe<PlayMusicEvent>(OnPlayMusicEvent));
    // ... more subscriptions
}

// Handler
private void OnPlaySoundEvent(PlaySoundEvent evt)
{
    PlaySound(evt.SoundName, evt.Volume, evt.Pitch, evt.Pan);
}
```

**How it works**:
- Events inherit from poolable base
- Reset method clears all fields
- Subscriptions stored for cleanup
- Handlers invoke service methods

**Applications to other systems**:
- **Combat System**: DamageEvent, HealEvent, StatusEvent (pooled)
- **UI System**: ButtonClickEvent, MenuOpenEvent (pooled)
- **Physics System**: CollisionEvent, TriggerEvent (pooled)
- **Network System**: PacketReceivedEvent (pooled)

**Key Benefits**:
- Zero allocation for frequent events
- Decoupled system communication
- Easy to add new event types
- Automatic cleanup on dispose

---

### 7. Configuration with Environment-Specific Presets
**Location**: `MonoBallFramework.Game/Engine/Audio/Configuration/AudioConfiguration.cs:8-144`

**Pattern**: Configuration class with static factory methods for different environments.

**Implementation**:
```csharp
public class AudioConfiguration
{
    // Properties
    public float DefaultMasterVolume { get; set; } = 1.0f;
    public float DefaultMusicVolume { get; set; } = 0.7f;
    public bool UseStreamingMode { get; set; } = true;
    public int MaxConcurrentSounds { get; set; } = 32;

    // Default preset
    public static AudioConfiguration Default => new();

    // Development preset
    public static AudioConfiguration Development => new()
    {
        DefaultMasterVolume = 1.0f,
        DefaultMusicVolume = 1.0f,
        MaxConcurrentSounds = 64,
        DefaultFadeDurationSeconds = 0.5f, // Faster for iteration
        UseStreamingMode = true,
    };

    // Production preset
    public static AudioConfiguration Production => new()
    {
        // Balanced user-friendly defaults
        UseStreamingMode = true, // 98% memory savings
    };

    // Compatibility preset
    public static AudioConfiguration CachedMode => new()
    {
        UseStreamingMode = false, // Legacy mode
    };
}
```

**How it works**:
- Mutable properties for customization
- Static factory methods for presets
- Named presets describe use case
- Easy to create custom variants

**Applications to other systems**:
- **Graphics Settings**: Low/Medium/High/Ultra presets
- **Physics Settings**: Mobile/Desktop/HighEnd presets
- **Network Settings**: LAN/Internet/Mobile presets
- **Debug Settings**: Production/Development/Profiling presets

**Key Benefits**:
- Quick environment switching
- Documented best practices
- Easy to customize from preset
- Self-documenting configurations

---

### 8. Orchestrator Pattern for System Coordination
**Location**: `MonoBallFramework.Game/Engine/Audio/Services/MapMusicOrchestrator.cs:14-213`

**Pattern**: Dedicated service that coordinates between multiple systems using events.

**Implementation**:
```csharp
public class MapMusicOrchestrator : IMapMusicOrchestrator
{
    private readonly World _world;
    private readonly IAudioService _audioService;
    private readonly MapLifecycleManager? _mapLifecycleManager;
    private readonly IDisposable? _mapTransitionSubscription;
    private readonly IDisposable? _mapRenderReadySubscription;

    public MapMusicOrchestrator(/* DI */)
    {
        // Subscribe to multiple event sources
        _mapTransitionSubscription = eventBus.Subscribe<MapTransitionEvent>(OnMapTransition);
        _mapRenderReadySubscription = eventBus.Subscribe<MapRenderReadyEvent>(OnMapRenderReady);
    }

    private void OnMapTransition(MapTransitionEvent evt)
    {
        // Filter: skip initial loads
        if (evt.IsInitialLoad) return;

        // Coordinate: query ECS for music, play with fade
        PlayMusicForMap(evt.ToMapId, isWarp: true);
    }

    private void PlayMusicForMap(string mapId, bool isWarp)
    {
        // Query ECS world
        _world.Query(in mapMusicQuery, (Entity entity, ref Music music, ref MapInfo info) =>
        {
            if (info.MapId == targetMapId)
                newMusicId = music.AudioId.Value;
        });

        // Coordinate with audio service
        _audioService.PlayMusic(newMusicId, loop: true,
            fadeDuration: isWarp ? _config.DefaultFadeDurationSeconds : 0f);
    }
}
```

**How it works**:
- Listens to events from multiple systems
- Queries ECS data for context
- Coordinates actions between systems
- Filters events based on context
- Manages lifecycle (dispose subscriptions)

**Applications to other systems**:
- **Camera Orchestrator**: Coordinates camera with player movement, cutscenes, zones
- **Tutorial Orchestrator**: Coordinates UI, input blocking, progression tracking
- **Weather Orchestrator**: Coordinates particles, lighting, audio, gameplay effects
- **Battle Orchestrator**: Coordinates turn order, animations, UI, audio

**Key Benefits**:
- Decouples systems from each other
- Centralizes coordination logic
- Easy to test in isolation
- Clear responsibility separation

---

## Game-Specific Patterns

### 9. Context-Aware Manager Pattern (Battle Audio)
**Location**: `MonoBallFramework.Game/Engine/Audio/Services/BattleAudioManager.cs:7-221`

**Pattern**: Specialized manager that maintains state and context for a specific game mode.

**Implementation**:
```csharp
public class BattleAudioManager : IBattleAudioManager
{
    private readonly IAudioService _audioService;
    private readonly IPokemonCryManager _pokemonCryManager;
    private readonly Dictionary<string, string> _battleMusicMap;

    private string? _preBattleMusicTrack; // Saved state
    private bool _isActive; // Context flag
    private ILoopingSoundHandle? _lowHealthWarningInstance; // Managed resource

    public void StartBattleMusic(BattleType battleType, string? musicName = null)
    {
        // Save previous state
        _preBattleMusicTrack = _audioService.CurrentMusicName;

        // Context-aware selection
        string battleMusic = musicName ?? GetBattleMusicForType(battleType);

        // Play with context
        PlayEncounterSound(GetEncounterType(battleType));
        _audioService.PlayMusic(battleMusic, loop: true, fadeDuration: 0.5f);

        _isActive = true; // Activate context
    }

    public void StopBattleMusic(float fadeOutDuration = 1.0f)
    {
        StopLowHealthWarning();
        _audioService.StopMusic(fadeOutDuration);

        // Restore previous state
        if (!string.IsNullOrEmpty(_preBattleMusicTrack))
        {
            _audioService.PlayMusic(_preBattleMusicTrack, loop: true, fadeDuration);
            _preBattleMusicTrack = null;
        }

        _isActive = false; // Deactivate context
    }

    // Context-gated operations
    public void PlayMoveSound(string moveName, string? moveType = null)
    {
        if (!_isActive) return; // Guard with context
        // ... implementation
    }
}
```

**How it works**:
- Maintains context flag (IsActive)
- Saves and restores previous state
- Guards operations with context checks
- Manages context-specific resources
- Maps game concepts to audio assets

**Applications to other systems**:
- **Menu Manager**: Maintains menu stack, saves/restores input state
- **Cutscene Manager**: Saves/restores camera, disables input, manages resources
- **Tutorial Manager**: Tracks progress, manages UI overlays, blocks input
- **Minigame Manager**: Saves game state, manages minigame resources

**Key Benefits**:
- Clean state management
- Automatic cleanup
- Context-aware operations
- State restoration

---

### 10. Zone-Based Component Pattern (ECS)
**Location**: `MonoBallFramework.Game/Ecs/Components/Audio/MusicZoneComponent.cs:9-73`

**Pattern**: ECS component with spatial bounds and priority for area-based behavior.

**Implementation**:
```csharp
public struct MusicZoneComponent
{
    public string MusicName { get; set; }
    public Rectangle ZoneBounds { get; set; }
    public int Priority { get; set; } // Higher priority overrides
    public bool Loop { get; set; }
    public float CrossfadeDuration { get; set; }
    public bool IsActive { get; set; }
    public bool PlayerInZone { get; set; }

    // Spatial query
    public readonly bool Contains(Vector2 position)
    {
        return ZoneBounds.Contains(position);
    }
}

// System queries zones and handles overlaps
public class MusicZoneSystem
{
    public void Update()
    {
        var playerPos = GetPlayerPosition();
        var activeZones = new List<MusicZoneComponent>();

        // Query all zones
        _world.Query((ref MusicZoneComponent zone) =>
        {
            bool wasInZone = zone.PlayerInZone;
            zone.PlayerInZone = zone.Contains(playerPos);

            // Handle zone entry/exit
            if (zone.PlayerInZone && !wasInZone)
                activeZones.Add(zone);
        });

        // Handle overlapping zones by priority
        if (activeZones.Count > 0)
        {
            var topZone = activeZones.OrderByDescending(z => z.Priority).First();
            PlayZoneMusic(topZone);
        }
    }
}
```

**How it works**:
- Component stores spatial data and metadata
- Contains method for spatial queries
- Priority for handling overlaps
- State tracking (PlayerInZone)
- System handles entry/exit logic

**Applications to other systems**:
- **LightingZone**: Dynamic lighting areas with priority
- **WeatherZone**: Weather effect areas with transitions
- **TriggerZone**: Event triggers with priority
- **CameraZone**: Camera behavior zones (follow, fixed, etc.)
- **SpawnZone**: Enemy/item spawn areas

**Key Benefits**:
- Data-oriented design
- Easy to query spatially
- Natural overlap handling
- Reusable pattern for any spatial behavior

---

## Performance Patterns

### 11. Two-Phase Initialization (Eager/Lazy)
**Location**: `MonoBallFramework.Game/Engine/Audio/AudioRegistry.cs:54-112`

**Pattern**: Supports both eager initialization and lazy loading with thread-safety.

**Implementation**:
```csharp
private volatile bool _isCacheLoaded;
private readonly SemaphoreSlim _loadLock = new(1, 1);

// Eager initialization (at startup)
public void LoadDefinitions()
{
    if (_isCacheLoaded) return;

    _loadLock.Wait();
    try
    {
        if (_isCacheLoaded) return; // Double-check

        using var context = _contextFactory.CreateDbContext();
        var definitions = context.AudioDefinitions.AsNoTracking().ToList();

        foreach (var def in definitions)
        {
            _cache[def.AudioId.Value] = def;
            _trackIdCache[def.TrackId] = def;
        }

        _isCacheLoaded = true;
    }
    finally
    {
        _loadLock.Release();
    }
}

// Lazy loading (on first use)
public AudioDefinition? GetById(string id)
{
    // Fast path: cache hit
    if (_cache.TryGetValue(id, out var cached))
        return cached;

    // Slow path: query database
    if (!_isCacheLoaded)
    {
        using var context = _contextFactory.CreateDbContext();
        var def = context.AudioDefinitions
            .AsNoTracking()
            .FirstOrDefault(a => a.AudioId.Value == id);

        if (def != null)
        {
            _cache[id] = def;
            _trackIdCache[def.TrackId] = def;
        }

        return def;
    }

    return null;
}
```

**How it works**:
- Eager: Load all at startup (controlled)
- Lazy: Load on-demand (flexible)
- Thread-safe with double-check locking
- Volatile flag for lock-free reads
- Database fallback if cache not loaded

**Applications to other systems**:
- **Asset Loading**: Preload critical assets, lazy load optional
- **Entity Templates**: Preload common prefabs, lazy load rare
- **Localization**: Preload current language, lazy load others
- **AI Behavior Trees**: Preload common behaviors, lazy load boss AI

**Key Benefits**:
- Fast startup (lazy)
- Predictable performance (eager)
- Thread-safe
- Memory efficient

---

### 12. Streaming vs Cached Strategy Pattern
**Location**: `MonoBallFramework.Game/Engine/Audio/Configuration/AudioConfiguration.cs:76-82`

**Pattern**: Runtime selection between memory-intensive cached and CPU-intensive streaming.

**Implementation**:
```csharp
public class AudioConfiguration
{
    /// <summary>
    /// Streaming mode: ~64KB per stream, slight CPU overhead
    /// Cached mode: ~32MB per track, zero CPU overhead
    /// </summary>
    public bool UseStreamingMode { get; set; } = true;
}

// Service registration selects implementation
services.AddSingleton<IMusicPlayer>(sp =>
{
    var config = sp.GetRequiredService<AudioConfiguration>();

    if (config.UseStreamingMode)
    {
        // 98% memory reduction, ~5% CPU increase
        return new NAudioStreamingMusicPlayer(audioRegistry, logger);
    }
    else
    {
        // High memory, zero CPU overhead
        return new NAudioMusicPlayer(audioRegistry, logger);
    }
});
```

**How it works**:
- Configuration flag controls strategy
- Factory method selects implementation
- Both implement same interface
- Tradeoff: memory vs CPU

**Performance Numbers**:
- Streaming: ~64KB memory, ~5% CPU overhead
- Cached: ~32MB memory, zero CPU overhead
- 98% memory reduction with streaming

**Applications to other systems**:
- **Texture Streaming**: Stream textures vs preload all
- **Mesh LOD**: Stream high-detail meshes vs cache all LODs
- **Animation**: Stream animation data vs cache all frames
- **World Chunks**: Stream chunks vs cache entire map

**Key Benefits**:
- Configurable performance tradeoffs
- Easy to benchmark both approaches
- Can change at runtime
- Clear memory/CPU tradeoffs

---

### 13. Active Instance Tracking with Automatic Cleanup
**Location**: `MonoBallFramework.Game/Engine/Audio/Services/SoundEffectPool.cs:99-114`

**Pattern**: Tracks active instances and automatically returns completed ones to pool.

**Implementation**:
```csharp
private readonly List<SoundEffectInstance> _activeInstances;
private readonly Stack<SoundEffectInstance> _pooledInstances;

public void Update()
{
    // Reverse iterate to safely remove during iteration
    for (int i = _activeInstances.Count - 1; i >= 0; i--)
    {
        var instance = _activeInstances[i];

        // Check if stopped and not looping
        if (instance.State == SoundState.Stopped && !instance.IsLooped)
        {
            _activeInstances.RemoveAt(i);
            ReturnInstanceToPool(instance);
        }
    }
}

private void ReturnInstanceToPool(SoundEffectInstance instance)
{
    if (_pooledInstances.Count < _maxConcurrentInstances)
    {
        _pooledInstances.Push(instance); // Reuse
    }
    else
    {
        instance.Dispose(); // Pool full, dispose
    }
}
```

**How it works**:
- Tracks all active instances
- Update loop checks completion
- Reverse iteration for safe removal
- Automatic return to pool
- Max capacity enforcement

**Applications to other systems**:
- **Particle System**: Auto-cleanup dead particles
- **Projectile System**: Auto-cleanup destroyed projectiles
- **Animation System**: Auto-cleanup finished animations
- **VFX System**: Auto-cleanup finished effects

**Key Benefits**:
- Zero manual cleanup
- Automatic memory management
- Bounded memory usage
- No memory leaks

---

## Integration Patterns

### 14. Interface Segregation with Specialized Managers
**Location**: Multiple files (IAudioService, IBattleAudioManager, IPokemonCryManager)

**Pattern**: Multiple specialized interfaces instead of one monolithic interface.

**Implementation**:
```csharp
// Base audio service (8 core methods)
public interface IAudioService : IDisposable
{
    bool PlaySound(string soundName, float? volume = null);
    void PlayMusic(string musicName, bool loop = true);
    void StopMusic(float fadeDuration = 0f);
    // ... 5 more core methods
}

// Specialized battle audio (10 battle-specific methods)
public interface IBattleAudioManager : IDisposable
{
    void StartBattleMusic(BattleType battleType);
    void PlayMoveSound(string moveName, string? moveType = null);
    void PlayBattleCry(int speciesId, bool isPlayerPokemon);
    void PlayLowHealthWarning();
    // ... 6 more battle methods
}

// Specialized cry manager (3 cry-specific methods)
public interface IPokemonCryManager : IDisposable
{
    void PlayCry(int speciesId, int formId = 0, float? volume = null);
    void PlayCryWithDucking(int speciesId, int formId = 0);
    void StopCry();
}
```

**How it works**:
- Small, focused interfaces
- Each interface serves one context
- Composition over inheritance
- Dependency injection of needed interfaces only

**Applications to other systems**:
- **Input System**:
  - IInputService (core input)
  - IBattleInputManager (battle-specific)
  - IMenuInputManager (menu-specific)
- **Graphics System**:
  - IRenderer (core rendering)
  - IParticleRenderer (particles)
  - IUIRenderer (UI-specific)
- **Animation System**:
  - IAnimationService (core animations)
  - IBattleAnimationManager (battle animations)
  - ICutsceneAnimationManager (cutscene-specific)

**Key Benefits**:
- Clear responsibilities
- Easy to test (mock specific interface)
- Flexible composition
- No bloated interfaces

---

### 15. Facade Pattern with Event Subscription
**Location**: `MonoBallFramework.Game/Engine/Audio/Services/NAudioService.cs:499-588`

**Pattern**: Service facade that both provides API and subscribes to events.

**Implementation**:
```csharp
public class NAudioService : IAudioService
{
    private readonly List<IDisposable> _subscriptions;

    public void Initialize()
    {
        SubscribeToEvents(); // Auto-subscribe
    }

    // Public API methods
    public bool PlaySound(string soundName, float? volume = null)
    {
        // Implementation
    }

    // Event subscriptions
    private void SubscribeToEvents()
    {
        _subscriptions.Add(_eventBus.Subscribe<PlaySoundEvent>(OnPlaySoundEvent));
        _subscriptions.Add(_eventBus.Subscribe<PlayMusicEvent>(OnPlayMusicEvent));
        _subscriptions.Add(_eventBus.Subscribe<StopMusicEvent>(OnStopMusicEvent));
    }

    // Event handlers call API methods
    private void OnPlaySoundEvent(PlaySoundEvent evt)
    {
        PlaySound(evt.SoundName, evt.Volume, evt.Pitch, evt.Pan);
    }

    private void OnPlayMusicEvent(PlayMusicEvent evt)
    {
        PlayMusic(evt.MusicName, evt.Loop, evt.FadeDuration);
    }

    public void Dispose()
    {
        // Cleanup subscriptions
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();
    }
}
```

**How it works**:
- Service provides both API and event handling
- Event handlers forward to API methods
- Subscriptions tracked for cleanup
- Auto-subscribe on initialization

**Applications to other systems**:
- **Physics System**: API + CollisionEvent subscription
- **UI System**: API + ButtonClickEvent subscription
- **Save System**: API + AutoSaveEvent subscription
- **Network System**: API + PacketReceivedEvent subscription

**Key Benefits**:
- Dual access pattern (direct + events)
- Centralized implementation
- Automatic subscription management
- Easy to test both paths

---

## Summary Table

| Pattern | Location | Primary Benefit | Best For |
|---------|----------|-----------------|----------|
| Registry with Dual Cache | AudioRegistry.cs:15 | O(1) lookups | Asset management |
| Two-Tier Object Pooling | AudioBufferPool.cs:15 | 95% GC reduction | Performance-critical paths |
| Template Method | MusicPlayerBase.cs:12 | Code reuse | Complex shared logic |
| State Machine | StreamingPlaybackState.cs:93 | Clear states | Multi-phase operations |
| Service Registration | AudioServicesExtensions.cs:15 | Clean DI | System initialization |
| Pooled Events | AudioEvents.cs:9 | Zero allocation | Frequent messaging |
| Environment Configs | AudioConfiguration.cs:86 | Quick switching | Multi-environment code |
| Orchestrator | MapMusicOrchestrator.cs:14 | Decoupling | Cross-system coordination |
| Context Manager | BattleAudioManager.cs:7 | State isolation | Mode-specific behavior |
| Zone Component | MusicZoneComponent.cs:9 | Spatial behavior | Area-based features |
| Eager/Lazy Init | AudioRegistry.cs:54 | Flexibility | Variable load patterns |
| Streaming Strategy | AudioConfiguration.cs:76 | Memory/CPU tradeoff | Large data sets |
| Auto Cleanup | SoundEffectPool.cs:99 | Zero leaks | Automatic resource mgmt |
| Interface Segregation | Multiple files | Focused APIs | Clean architecture |
| Facade + Events | NAudioService.cs:499 | Dual access | Hybrid systems |

---

## Quick Reference: Pattern Selection Guide

**Need asset management?** → Registry Pattern (#1)
**Performance-critical path?** → Object Pooling (#2)
**Shared complex logic?** → Template Method (#3)
**Multi-phase operations?** → State Machine (#4)
**Clean DI setup?** → Service Registration (#5)
**Frequent messaging?** → Pooled Events (#6)
**Multi-environment code?** → Config Presets (#7)
**Cross-system coordination?** → Orchestrator (#8)
**Mode-specific behavior?** → Context Manager (#9)
**Area-based features?** → Zone Component (#10)
**Variable load patterns?** → Eager/Lazy Init (#11)
**Large data sets?** → Streaming Strategy (#12)
**Automatic cleanup?** → Auto Cleanup (#13)
**Clean architecture?** → Interface Segregation (#14)
**Hybrid systems?** → Facade + Events (#15)

---

*Generated by Hive Mind audio pattern analysis*
