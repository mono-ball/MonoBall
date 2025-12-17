using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities.Base;

namespace MonoBallFramework.Game.GameData.Entities;

/// <summary>
///     EF Core entity for audio definitions (music tracks and sound effects).
///     Stores audio track metadata loaded from JSON definition files.
/// </summary>
[Table("Audios")]
public class AudioEntity : BaseEntity
{
    /// <summary>
    ///     Unique audio identifier in unified format.
    ///     Example: "base:audio:music/towns/mus_dewford"
    /// </summary>
    [Key]
    [MaxLength(150)]
    [Column(TypeName = "nvarchar(150)")]
    public GameAudioId AudioId { get; set; } = null!;

    /// <summary>
    ///     Path to the audio file relative to Assets folder.
    ///     Example: "Audio/Music/Towns/mus_dewford.ogg"
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string AudioPath { get; set; } = string.Empty;

    /// <summary>
    ///     Audio category (e.g., "music", "sfx").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "music";

    /// <summary>
    ///     Audio subcategory (e.g., "towns", "battle", "routes").
    /// </summary>
    [MaxLength(50)]
    public string? Subcategory { get; set; }

    /// <summary>
    ///     Default volume for this track (0.0 - 1.0).
    /// </summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>
    ///     Whether this track should loop.
    /// </summary>
    public bool Loop { get; set; } = true;

    /// <summary>
    ///     Default fade-in duration in seconds.
    /// </summary>
    public float FadeIn { get; set; }

    /// <summary>
    ///     Default fade-out duration in seconds.
    /// </summary>
    public float FadeOut { get; set; }

    /// <summary>
    ///     Loop start position in samples (at 44100 Hz).
    ///     If null, loops from the beginning of the track.
    /// </summary>
    public int? LoopStartSamples { get; set; }

    /// <summary>
    ///     Loop length in samples (at 44100 Hz).
    ///     If null, loops to the end of the track.
    /// </summary>
    public int? LoopLengthSamples { get; set; }

    /// <summary>
    ///     Loop start position in seconds (for debugging/display).
    /// </summary>
    public float? LoopStartSec { get; set; }

    /// <summary>
    ///     Loop end position in seconds (for debugging/display).
    /// </summary>
    public float? LoopEndSec { get; set; }

    // Computed properties for convenience (not stored in DB)

    /// <summary>
    ///     Gets whether this track has defined loop points.
    /// </summary>
    [NotMapped]
    public bool HasLoopPoints => LoopStartSamples.HasValue && LoopLengthSamples.HasValue;

    /// <summary>
    ///     Gets the track ID (short name) from the full audio ID.
    ///     Example: "mus_dewford" from "base:audio:music/towns/mus_dewford"
    /// </summary>
    [NotMapped]
    public string TrackId => AudioId.Name;

    /// <summary>
    ///     Gets whether this is a music track (vs SFX).
    /// </summary>
    [NotMapped]
    public bool IsMusic => Category.Equals("music", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets whether this is a sound effect.
    /// </summary>
    [NotMapped]
    public bool IsSoundEffect => Category.Equals("sfx", StringComparison.OrdinalIgnoreCase);
}
