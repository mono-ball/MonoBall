using Arch.Core;
using Arch.Core.Extensions;
using MonoBallFramework.Game.Ecs.Components.Audio;
using MonoBallFramework.Game.Engine.Audio.Services;
using Microsoft.Xna.Framework;

namespace MonoBallFramework.Game.Engine.Audio.Systems;

/// <summary>
///     ECS system that manages ambient sound components.
///     Handles volume fading and distance-based attenuation for ambient sounds.
/// </summary>
public class AmbientSoundSystem
{
    private readonly World _world;
    private readonly IAudioService _audioService;
    private readonly QueryDescription _ambientQuery;

    private Vector2 _listenerPosition;
    private readonly Dictionary<Entity, ILoopingSoundHandle?> _activeInstances;

    public AmbientSoundSystem(World world, IAudioService audioService)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));

        _ambientQuery = new QueryDescription()
            .WithAll<AmbientSoundComponent>();

        _activeInstances = new Dictionary<Entity, ILoopingSoundHandle?>();
    }

    /// <summary>
    ///     Sets the listener position for distance-based volume calculations.
    /// </summary>
    public void SetListenerPosition(Vector2 position)
    {
        _listenerPosition = position;
    }

    /// <summary>
    ///     Updates all ambient sounds, managing their volumes and playback.
    /// </summary>
    public void Update(float deltaTime)
    {
        var entitiesToUpdate = new List<(Entity entity, AmbientSoundComponent component)>();

        _world.Query(in _ambientQuery, (Entity entity, ref AmbientSoundComponent ambient) =>
        {
            entitiesToUpdate.Add((entity, ambient));
        });

        foreach (var (entity, ambient) in entitiesToUpdate)
        {
            UpdateAmbientSound(entity, ambient, deltaTime);
        }
    }

    private void UpdateAmbientSound(Entity entity, AmbientSoundComponent ambient, float deltaTime)
    {
        if (!ambient.IsActive)
        {
            StopAmbientSound(entity);
            return;
        }

        // Check if player is in the zone (if zone bounds are defined)
        bool inZone = true;
        if (ambient.ZoneBounds.HasValue)
        {
            inZone = ambient.ZoneBounds.Value.Contains(_listenerPosition);
        }

        float targetVolume = inZone ? ambient.BaseVolume : 0f;

        // Apply distance-based attenuation if MaxDistance is set
        if (ambient.MaxDistance > 0 && inZone)
        {
            // For zone-based ambient, we'd need the zone center
            // For simplicity, we'll use the zone bounds center if available
            if (ambient.ZoneBounds.HasValue)
            {
                Vector2 zoneCenter = ambient.ZoneBounds.Value.Center.ToVector2();
                float distance = Vector2.Distance(_listenerPosition, zoneCenter);

                if (distance > ambient.MaxDistance)
                {
                    targetVolume = 0f;
                }
                else if (distance > 0)
                {
                    float attenuation = 1f - (distance / ambient.MaxDistance);
                    targetVolume *= attenuation;
                }
            }
        }

        // Update current volume with fading
        if (ambient.UseFading && ambient.FadeDuration > 0)
        {
            float fadeSpeed = 1f / ambient.FadeDuration;
            float volumeDelta = fadeSpeed * deltaTime;

            if (ambient.CurrentVolume < targetVolume)
            {
                ambient.CurrentVolume = Math.Min(ambient.CurrentVolume + volumeDelta, targetVolume);
            }
            else if (ambient.CurrentVolume > targetVolume)
            {
                ambient.CurrentVolume = Math.Max(ambient.CurrentVolume - volumeDelta, targetVolume);
            }
        }
        else
        {
            ambient.CurrentVolume = targetVolume;
        }

        // Manage sound instance
        if (ambient.CurrentVolume > 0.01f)
        {
            EnsureAmbientSoundPlaying(entity, ambient);
        }
        else
        {
            StopAmbientSound(entity);
        }

        // Update the component in the world
        _world.Set(entity, ambient);
    }

    private void EnsureAmbientSoundPlaying(Entity entity, AmbientSoundComponent ambient)
    {
        if (!_activeInstances.TryGetValue(entity, out var instance) || instance == null)
        {
            // Create new looping instance
            instance = _audioService.PlayLoopingSound(ambient.SoundName, ambient.CurrentVolume);
            _activeInstances[entity] = instance;
        }
        else
        {
            // Update existing instance volume
            instance.Volume = ambient.CurrentVolume;
        }
    }

    private void StopAmbientSound(Entity entity)
    {
        if (_activeInstances.TryGetValue(entity, out var instance) && instance != null)
        {
            _audioService.StopLoopingSound(instance);
            _activeInstances.Remove(entity);
        }
    }

    /// <summary>
    ///     Cleans up all active ambient sound instances.
    /// </summary>
    public void Cleanup()
    {
        foreach (var instance in _activeInstances.Values)
        {
            if (instance != null)
            {
                _audioService.StopLoopingSound(instance);
            }
        }
        _activeInstances.Clear();
    }
}
