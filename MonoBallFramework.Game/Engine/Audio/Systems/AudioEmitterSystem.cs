using Arch.Core;
using Arch.Core.Extensions;
using MonoBallFramework.Game.Ecs.Components.Audio;
using MonoBallFramework.Game.Engine.Audio.Services;
using Microsoft.Xna.Framework;

namespace MonoBallFramework.Game.Engine.Audio.Systems;

/// <summary>
///     ECS system that manages positional audio emitters.
///     Updates audio emitter volumes and panning based on distance from the listener.
/// </summary>
public class AudioEmitterSystem
{
    private readonly World _world;
    private readonly IAudioService _audioService;
    private readonly QueryDescription _emitterQuery;

    private Vector2 _listenerPosition;

    public AudioEmitterSystem(World world, IAudioService audioService)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));

        _emitterQuery = new QueryDescription()
            .WithAll<AudioEmitterComponent>();
    }

    /// <summary>
    ///     Sets the listener position for 3D audio calculations.
    /// </summary>
    public void SetListenerPosition(Vector2 position)
    {
        _listenerPosition = position;
    }

    /// <summary>
    ///     Updates all audio emitters based on listener position.
    /// </summary>
    public void Update(float deltaTime)
    {
        _world.Query(in _emitterQuery, (ref AudioEmitterComponent emitter) =>
        {
            if (!emitter.IsActive)
                return;

            // Calculate volume and pan based on distance
            float volume = emitter.CalculateVolume(_listenerPosition);
            float pan = emitter.CalculatePan(_listenerPosition);

            // If volume is effectively zero, we can skip playing
            if (volume < 0.01f)
                return;

            // For looping emitters, this should be managed differently
            // (create persistent instances that update their properties)
            // For now, we'll just trigger sounds when appropriate
            if (!emitter.IsLooping)
            {
                _audioService.PlaySound(emitter.SoundName, volume, null, pan);
            }
        });
    }
}
