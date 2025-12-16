using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.Entities;

/// <summary>
///     EF Core entity for popup background definitions.
///     Stores bitmap background styles for map region/location popups.
///     Replaces direct JSON loading in PopupRegistry.
/// </summary>
[Table("PopupBackgrounds")]
public class PopupBackgroundEntity
{
    /// <summary>
    ///     Unique identifier for this background style.
    ///     Example: "base:popup:background/wood"
    /// </summary>
    [Key]
    [Column(TypeName = "nvarchar(150)")]
    public GamePopupBackgroundId BackgroundId { get; set; } = GamePopupBackgroundId.Create("default");

    /// <summary>
    ///     Display name for this background style.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Type of rendering (always "Bitmap" for backgrounds).
    /// </summary>
    [MaxLength(50)]
    public string Type { get; set; } = "Bitmap";

    /// <summary>
    ///     Path to the background texture (relative to asset root).
    ///     Example: "Graphics/Maps/Popups/Backgrounds/stone.png"
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string TexturePath { get; set; } = string.Empty;

    /// <summary>
    ///     Width of the source bitmap in pixels (typically 80 for pokeemerald).
    /// </summary>
    public int Width { get; set; } = 80;

    /// <summary>
    ///     Height of the source bitmap in pixels (typically 24 for pokeemerald).
    /// </summary>
    public int Height { get; set; } = 24;

    /// <summary>
    ///     Optional description of this background style.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

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

    // Computed property for theme name extraction

    /// <summary>
    ///     Gets the theme name from the ID (e.g., "wood" from "base:popup:background/wood")
    /// </summary>
    [NotMapped]
    public string ThemeName => BackgroundId.Name;
}
