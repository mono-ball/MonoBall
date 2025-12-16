using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.Entities;

/// <summary>
///     EF Core entity for map section (MAPSEC) definitions.
///     Defines region map areas and popup themes for map locations.
/// </summary>
[Table("MapSections")]
public class MapSection
{
    /// <summary>
    ///     Unique MAPSEC identifier in unified format (e.g., "base:mapsec:hoenn/littleroot_town").
    /// </summary>
    [Key]
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public GameMapSectionId MapSectionId { get; set; } = null!;

    /// <summary>
    ///     Display name for region map (e.g., "LITTLEROOT TOWN").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Popup theme ID reference in unified format (e.g., "base:theme:popup/wood").
    /// </summary>
    [Required]
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public GameThemeId ThemeId { get; set; } = null!;

    /// <summary>
    ///     X position on region map grid (8x8 pixel tiles).
    /// </summary>
    public int? X { get; set; }

    /// <summary>
    ///     Y position on region map grid (8x8 pixel tiles).
    /// </summary>
    public int? Y { get; set; }

    /// <summary>
    ///     Width on region map (in tiles).
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    ///     Height on region map (in tiles).
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    ///     Source mod ID (null for base game).
    /// </summary>
    [MaxLength(100)]
    public string? SourceMod { get; set; }

    /// <summary>
    ///     Version for compatibility tracking.
    /// </summary>
    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";

    // Navigation property
    /// <summary>
    ///     Popup theme for this section.
    /// </summary>
    [ForeignKey(nameof(ThemeId))]
    public PopupTheme? Theme { get; set; }
}



