using Arch.Core;
using Arch.Core.Extensions;
using MonoBallFramework.Game.Ecs.Components.Audio;
using MonoBallFramework.Game.Engine.Audio.Services;
using Microsoft.Xna.Framework;

namespace MonoBallFramework.Game.Engine.Audio.Systems;

/// <summary>
///     ECS system that manages music zones.
///     Automatically changes background music when the player enters/exits zones.
/// </summary>
public class MusicZoneSystem
{
    private readonly World _world;
    private readonly IAudioService _audioService;
    private readonly QueryDescription _zoneQuery;

    private Vector2 _playerPosition;
    private MusicZoneComponent? _activeZone;
    private bool _hasActiveZone;

    public MusicZoneSystem(World world, IAudioService audioService)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));

        _zoneQuery = new QueryDescription()
            .WithAll<MusicZoneComponent>();
    }

    /// <summary>
    ///     Sets the player's current position for zone detection.
    /// </summary>
    public void SetPlayerPosition(Vector2 position)
    {
        _playerPosition = position;
    }

    /// <summary>
    ///     Updates music zones and triggers music changes when entering/exiting zones.
    /// </summary>
    public void Update(float deltaTime)
    {
        MusicZoneComponent? highestPriorityZone = null;
        bool playerInAnyZone = false;

        // Find the highest priority zone containing the player
        _world.Query(in _zoneQuery, (ref MusicZoneComponent zone) =>
        {
            if (!zone.IsActive)
                return;

            bool wasInZone = zone.PlayerInZone;
            bool isInZone = zone.Contains(_playerPosition);

            zone.PlayerInZone = isInZone;

            if (isInZone)
            {
                playerInAnyZone = true;

                if (highestPriorityZone == null || zone.Priority > highestPriorityZone.Value.Priority)
                {
                    highestPriorityZone = zone;
                }

                // Handle zone entry
                if (!wasInZone)
                {
                    OnZoneEntered(zone);
                }
            }
            else if (wasInZone)
            {
                // Handle zone exit
                OnZoneExited(zone);
            }
        });

        // Update active zone and trigger music changes
        if (playerInAnyZone && highestPriorityZone.HasValue)
        {
            var newZone = highestPriorityZone.Value;

            // Check if we need to change music
            if (!_hasActiveZone || (_activeZone.HasValue && !AreSameZone(_activeZone.Value, newZone)))
            {
                SwitchToZoneMusic(newZone);
                _activeZone = newZone;
                _hasActiveZone = true;
            }
        }
        else if (_hasActiveZone)
        {
            // Player left all zones
            _hasActiveZone = false;
            _activeZone = null;
            // Optionally restore default music here
        }
    }

    private void OnZoneEntered(MusicZoneComponent zone)
    {
        // TODO: Add zone entry logic if needed (trigger events, analytics, etc.)
    }

    private void OnZoneExited(MusicZoneComponent zone)
    {
        // TODO: Add zone exit logic if needed (trigger events, analytics, etc.)
    }

    private void SwitchToZoneMusic(MusicZoneComponent zone)
    {
        if (string.IsNullOrEmpty(zone.MusicName))
            return;

        // Check if this music is already playing
        if (_audioService.CurrentMusicName == zone.MusicName)
            return;

        _audioService.PlayMusic(zone.MusicName, zone.Loop, zone.CrossfadeDuration);
    }

    private bool AreSameZone(MusicZoneComponent a, MusicZoneComponent b)
    {
        return a.MusicName == b.MusicName &&
               a.ZoneBounds == b.ZoneBounds &&
               a.Priority == b.Priority;
    }
}
