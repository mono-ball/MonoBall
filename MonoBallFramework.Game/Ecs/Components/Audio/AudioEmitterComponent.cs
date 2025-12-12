using Microsoft.Xna.Framework;

namespace MonoBallFramework.Game.Ecs.Components.Audio;

/// <summary>
///     ECS component for positional audio sources (3D audio simulation).
///     Used for sounds that change volume/pan based on distance from the player.
/// </summary>
public struct AudioEmitterComponent
{
    /// <summary>
    ///     Gets or sets the position of the audio emitter in world space.
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    ///     Gets or sets the name/path of the sound being emitted.
    /// </summary>
    public string SoundName { get; set; }

    /// <summary>
    ///     Gets or sets the maximum distance at which the sound can be heard (in tiles).
    /// </summary>
    public float MaxDistance { get; set; }

    /// <summary>
    ///     Gets or sets the reference distance for volume calculations (in tiles).
    ///     At this distance or closer, the sound plays at full volume.
    /// </summary>
    public float ReferenceDistance { get; set; }

    /// <summary>
    ///     Gets or sets the base volume (0.0 to 1.0) at reference distance.
    /// </summary>
    public float BaseVolume { get; set; }

    /// <summary>
    ///     Gets or sets whether to apply panning based on horizontal position.
    /// </summary>
    public bool UsePanning { get; set; }

    /// <summary>
    ///     Gets or sets whether the emitter's sound should loop continuously.
    /// </summary>
    public bool IsLooping { get; set; }

    /// <summary>
    ///     Gets or sets whether the emitter is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Gets or sets the rolloff factor for distance-based volume attenuation.
    ///     Higher values = faster falloff with distance.
    /// </summary>
    public float RolloffFactor { get; set; }

    /// <summary>
    ///     Initializes a new audio emitter component.
    /// </summary>
    /// <param name="position">The world position of the emitter.</param>
    /// <param name="soundName">The name/path of the sound.</param>
    /// <param name="maxDistance">Maximum audible distance in tiles.</param>
    /// <param name="volume">Base volume (0.0 to 1.0).</param>
    public AudioEmitterComponent(
        Vector2 position,
        string soundName,
        float maxDistance = 10f,
        float volume = 1.0f)
    {
        Position = position;
        SoundName = soundName;
        MaxDistance = maxDistance;
        ReferenceDistance = 2f;
        BaseVolume = volume;
        UsePanning = true;
        IsLooping = true;
        IsActive = true;
        RolloffFactor = 1.0f;
    }

    /// <summary>
    ///     Calculates the volume based on distance from listener.
    /// </summary>
    /// <param name="listenerPosition">The listener's position.</param>
    /// <returns>The calculated volume (0.0 to 1.0).</returns>
    public readonly float CalculateVolume(Vector2 listenerPosition)
    {
        float distance = Vector2.Distance(Position, listenerPosition);

        if (distance <= ReferenceDistance)
            return BaseVolume;

        if (distance >= MaxDistance)
            return 0f;

        // Linear rolloff with configurable factor
        float normalizedDistance = (distance - ReferenceDistance) / (MaxDistance - ReferenceDistance);
        float attenuation = 1f - (normalizedDistance * RolloffFactor);

        return Math.Max(0f, BaseVolume * attenuation);
    }

    /// <summary>
    ///     Calculates the pan based on horizontal position relative to listener.
    /// </summary>
    /// <param name="listenerPosition">The listener's position.</param>
    /// <returns>The calculated pan (-1.0 left to 1.0 right).</returns>
    public readonly float CalculatePan(Vector2 listenerPosition)
    {
        if (!UsePanning)
            return 0f;

        float horizontalDelta = Position.X - listenerPosition.X;
        float panRange = MaxDistance * 0.5f; // Pan falls off faster than volume

        return Math.Clamp(horizontalDelta / panRange, -1f, 1f);
    }
}
