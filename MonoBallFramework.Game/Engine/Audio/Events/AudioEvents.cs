using MonoBallFramework.Game.Engine.Core.Events;

namespace MonoBallFramework.Game.Engine.Audio.Events;

/// <summary>
///     Event to request playing a sound effect.
///     Poolable for high-frequency usage.
/// </summary>
public class PlaySoundEvent : NotificationEventBase
{
    /// <summary>
    ///     Gets or sets the name/path of the sound to play.
    /// </summary>
    public string SoundName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the sound category for volume mixing.
    /// </summary>
    public SoundCategory Category { get; set; } = SoundCategory.UI;

    /// <summary>
    ///     Gets or sets the volume (0.0 to 1.0). Default is 1.0.
    /// </summary>
    public float Volume { get; set; } = 1f;

    /// <summary>
    ///     Gets or sets the pitch adjustment (-1.0 to 1.0). Default is 0.0.
    /// </summary>
    public float Pitch { get; set; }

    /// <summary>
    ///     Gets or sets the pan adjustment (-1.0 left to 1.0 right). Default is 0.0.
    /// </summary>
    public float Pan { get; set; }

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

/// <summary>
///     Event for playing background music.
/// </summary>
public class PlayMusicEvent : NotificationEventBase
{
    /// <summary>
    ///     Gets or sets the name/path of the music track to play.
    /// </summary>
    public string MusicName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets whether the music should loop.
    /// </summary>
    public bool Loop { get; set; } = true;

    /// <summary>
    ///     Gets or sets the fade-in duration in seconds (0 for instant).
    /// </summary>
    public float FadeDuration { get; set; }

    public override void Reset()
    {
        base.Reset();
        MusicName = string.Empty;
        Loop = true;
        FadeDuration = 0f;
    }
}

/// <summary>
///     Event for stopping background music.
/// </summary>
public class StopMusicEvent : NotificationEventBase
{
    /// <summary>
    ///     Gets or sets the fade-out duration in seconds (0 for instant).
    /// </summary>
    public float FadeDuration { get; set; }

    public override void Reset()
    {
        base.Reset();
        FadeDuration = 0f;
    }
}

/// <summary>
///     Event for crossfading between music tracks.
/// </summary>
public class FadeMusicEvent : NotificationEventBase
{
    /// <summary>
    ///     Gets or sets the name/path of the new music track.
    /// </summary>
    public string NewMusicName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the crossfade duration in seconds.
    /// </summary>
    public float CrossfadeDuration { get; set; } = 1.0f;

    /// <summary>
    ///     Gets or sets whether the new track should loop.
    /// </summary>
    public bool Loop { get; set; } = true;

    public override void Reset()
    {
        base.Reset();
        NewMusicName = string.Empty;
        CrossfadeDuration = 1.0f;
        Loop = true;
    }
}

/// <summary>
///     Event for playing a Pokemon cry.
/// </summary>
public class PlayPokemonCryEvent : NotificationEventBase
{
    /// <summary>
    ///     Gets or sets the Pokemon species ID (National Dex number).
    /// </summary>
    public int SpeciesId { get; set; }

    /// <summary>
    ///     Gets or sets the form ID (0 for base form).
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    ///     Gets or sets the volume override (0.0 to 1.0), or null to use default.
    /// </summary>
    public float? Volume { get; set; }

    /// <summary>
    ///     Gets or sets the pitch adjustment (-1.0 to 1.0), or null for no adjustment.
    /// </summary>
    public float? Pitch { get; set; }

    public override void Reset()
    {
        base.Reset();
        SpeciesId = 0;
        FormId = 0;
        Volume = null;
        Pitch = null;
    }
}

/// <summary>
///     Event for playing a battle move sound.
/// </summary>
public class PlayMoveSoundEvent : NotificationEventBase
{
    /// <summary>
    ///     Gets or sets the name of the move.
    /// </summary>
    public string MoveName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the type of the move for type-specific sound variations.
    /// </summary>
    public string? MoveType { get; set; }

    public override void Reset()
    {
        base.Reset();
        MoveName = string.Empty;
        MoveType = null;
    }
}

/// <summary>
///     Event for pausing background music.
/// </summary>
public class PauseMusicEvent : NotificationEventBase
{
}

/// <summary>
///     Event for resuming background music.
/// </summary>
public class ResumeMusicEvent : NotificationEventBase
{
}

/// <summary>
///     Event for stopping all sound effects.
/// </summary>
public class StopAllSoundsEvent : NotificationEventBase
{
}
