using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MonoBallFramework.Game.GameData.Entities.Base;

/// <summary>
///     Base class for all game data entities.
///     Provides common properties for mod tracking and versioning.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    ///     Source mod ID (null for base game).
    ///     Used to track which mod contributed this entity.
    /// </summary>
    [MaxLength(100)]
    public string? SourceMod { get; set; }

    /// <summary>
    ///     Version for compatibility tracking.
    ///     Enables backward compatibility checks and migrations.
    /// </summary>
    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    ///     Gets whether this entity is from a mod (not base game).
    /// </summary>
    [NotMapped]
    public bool IsFromMod => !string.IsNullOrEmpty(SourceMod);
}
