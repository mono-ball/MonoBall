using Arch.Core;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Engine.Audio.Configuration;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Events.Map;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.Systems;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Service that manages map background music.
///     Listens for map transition and render ready events and plays the appropriate music track
///     based on the Music component attached to map entities.
/// </summary>
public class MapMusicOrchestrator : IMapMusicOrchestrator
{
    private readonly IAudioService _audioService;
    private readonly AudioConfiguration _config;
    private readonly ILogger<MapMusicOrchestrator>? _logger;
    private readonly MapLifecycleManager? _mapLifecycleManager;
    private readonly IDisposable? _mapRenderReadySubscription;
    private readonly IDisposable? _mapTransitionSubscription;
    private readonly World _world;

    private string? _currentMapMusicId;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MapMusicOrchestrator" /> class.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="audioService">The audio service for playing music.</param>
    /// <param name="eventBus">The event bus for subscribing to map events.</param>
    /// <param name="mapLifecycleManager">Map lifecycle manager to check current map (optional).</param>
    /// <param name="config">Audio configuration (optional).</param>
    /// <param name="logger">Logger for diagnostics (optional).</param>
    public MapMusicOrchestrator(
        World world,
        IAudioService audioService,
        IEventBus eventBus,
        MapLifecycleManager? mapLifecycleManager = null,
        AudioConfiguration? config = null,
        ILogger<MapMusicOrchestrator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(audioService);
        ArgumentNullException.ThrowIfNull(eventBus);

        _world = world;
        _audioService = audioService;
        _mapLifecycleManager = mapLifecycleManager;
        _config = config ?? AudioConfiguration.Default;
        _logger = logger;

        // Subscribe to map transition events (for warps and boundary crossings)
        _mapTransitionSubscription = eventBus.Subscribe<MapTransitionEvent>(OnMapTransition);

        // Subscribe to map render ready events (for initial load)
        _mapRenderReadySubscription = eventBus.Subscribe<MapRenderReadyEvent>(OnMapRenderReady);

        _logger?.LogInformation("MapMusicOrchestrator initialized and subscribed to map events");
    }

    /// <summary>
    ///     Disposes of the orchestrator and unsubscribes from events.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        GC.SuppressFinalize(this);
        _mapTransitionSubscription?.Dispose();
        _mapRenderReadySubscription?.Dispose();
        _disposed = true;

        _logger?.LogDebug("MapMusicOrchestrator disposed");
    }

    /// <summary>
    ///     Handles map transition events (warps and boundary crossings).
    /// </summary>
    private void OnMapTransition(MapTransitionEvent evt)
    {
        _logger?.LogDebug(
            "Received MapTransitionEvent - From: {FromMap} -> To: {ToMap}",
            evt.FromMapName ?? "None",
            evt.ToMapName
        );

        // Skip initial map load - MapRenderReadyEvent will handle it
        if (evt.IsInitialLoad)
        {
            _logger?.LogDebug("Skipping music change for initial map load (MapRenderReadyEvent will handle it)");
            return;
        }

        // Handle music for warp transitions
        if (evt.ToMapId is not null)
        {
            PlayMusicForMap(evt.ToMapId, true);
        }
    }

    /// <summary>
    ///     Handles map render ready events (after first frame is rendered).
    ///     This is the ideal time to start music for initial map load.
    ///     Only plays music for the current/primary map, ignoring adjacent maps.
    /// </summary>
    private void OnMapRenderReady(MapRenderReadyEvent evt)
    {
        _logger?.LogDebug(
            "Received MapRenderReadyEvent - Map: {MapName} (ID: {MapId})",
            evt.MapName,
            evt.MapId
        );

        // Filter: Only play music for the current map (not adjacent maps)
        // Adjacent maps fire MapRenderReadyEvent too during seamless map streaming
        if (_mapLifecycleManager != null)
        {
            GameMapId? currentMapId = _mapLifecycleManager.CurrentMapId;
            if (currentMapId != null && currentMapId.Value != evt.MapId)
            {
                _logger?.LogDebug(
                    "Ignoring MapRenderReadyEvent for adjacent map {MapId} (current map: {CurrentMapId})",
                    evt.MapId,
                    currentMapId.Value
                );
                return;
            }
        }

        PlayMusicForMap(evt.MapId, false);
    }

    /// <summary>
    ///     Finds and plays the music for the specified map.
    /// </summary>
    /// <param name="mapIdStr">The map ID string.</param>
    /// <param name="isWarp">Whether this is triggered by a warp (use crossfade).</param>
    private void PlayMusicForMap(string mapIdStr, bool isWarp)
    {
        try
        {
            GameMapId targetMapId = new(mapIdStr);
            string? newMusicId = null;
            int mapsWithMusicCount = 0;

            // Query for the map entity with Music component
            QueryDescription mapMusicQuery = QueryCache.Get<Music, MapInfo>();
            _world.Query(in mapMusicQuery, (Entity entity, ref Music music, ref MapInfo mapInfo) =>
            {
                mapsWithMusicCount++;
                _logger?.LogDebug(
                    "Found map with music: {MapId} (looking for {TargetMapId}), Music: {MusicId}",
                    mapInfo.MapId.Value, targetMapId.Value, music.AudioId.Value);

                if (mapInfo.MapId == targetMapId)
                {
                    newMusicId = music.AudioId.Value;
                }
            });

            _logger?.LogDebug("Found {Count} maps with Music component", mapsWithMusicCount);

            if (!string.IsNullOrEmpty(newMusicId))
            {
                PlayMapMusic(newMusicId, isWarp);
            }
            else
            {
                _logger?.LogWarning("Map {MapId} has no music assigned (searched {Count} maps)", mapIdStr,
                    mapsWithMusicCount);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to play music for map {MapId}", mapIdStr);
        }
    }

    /// <summary>
    ///     Plays the specified map music with appropriate fade behavior.
    /// </summary>
    /// <param name="musicId">The music track ID to play.</param>
    /// <param name="isWarp">Whether this is triggered by a warp (use crossfade).</param>
    private void PlayMapMusic(string musicId, bool isWarp)
    {
        // Don't restart if already playing the same track
        if (_currentMapMusicId == musicId && _audioService.IsMusicPlaying)
        {
            _logger?.LogTrace("Music {MusicId} already playing, skipping", musicId);
            return;
        }

        // Determine fade duration based on transition type
        float fadeDuration = isWarp
            ? _config.DefaultFadeDurationSeconds
            : 0f; // Instant for initial load

        _audioService.PlayMusic(musicId, true, fadeDuration);
        _currentMapMusicId = musicId;

        _logger?.LogInformation(
            "Playing map music: {MusicId} (fade: {FadeDuration}s, warp: {IsWarp})",
            musicId, fadeDuration, isWarp);
    }
}
