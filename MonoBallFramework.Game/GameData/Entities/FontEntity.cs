using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities.Base;

namespace MonoBallFramework.Game.GameData.Entities;

/// <summary>
///     EF Core entity for font definitions.
///     Stores font metadata loaded from JSON definition files.
/// </summary>
[Table("Fonts")]
public class FontEntity : BaseEntity
{
    /// <summary>
    ///     Unique font identifier in unified format.
    ///     Example: "base:font:game/pokemon" or "base:font:debug/mono"
    /// </summary>
    [Key]
    [MaxLength(150)]
    [Column(TypeName = "nvarchar(150)")]
    public GameFontId FontId { get; set; } = null!;

    /// <summary>
    ///     Path to the font file relative to Assets/Fonts folder.
    ///     Example: "pokemon.ttf" or "0xProtoNerdFontMono-Regular.ttf"
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string FontPath { get; set; } = string.Empty;

    /// <summary>
    ///     Font usage category.
    ///     Examples: "game", "debug", "ui", "dialogue"
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "game";

    /// <summary>
    ///     Default font size in pixels.
    /// </summary>
    public int DefaultSize { get; set; } = 16;

    /// <summary>
    ///     Line spacing multiplier (1.0 = normal).
    /// </summary>
    public float LineSpacing { get; set; } = 1.0f;

    /// <summary>
    ///     Character spacing in pixels.
    /// </summary>
    public float CharacterSpacing { get; set; }

    /// <summary>
    ///     Whether this font supports extended Unicode characters.
    /// </summary>
    public bool SupportsUnicode { get; set; } = true;

    /// <summary>
    ///     Whether this is a monospace font.
    /// </summary>
    public bool IsMonospace { get; set; }

    // Computed properties (not stored in DB)

    /// <summary>
    ///     Gets the font name from the full font ID.
    /// </summary>
    [NotMapped]
    public string FontName => FontId.Name;

    /// <summary>
    ///     Gets whether this is a game font.
    /// </summary>
    [NotMapped]
    public bool IsGameFont => Category.Equals("game", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets whether this is a debug font.
    /// </summary>
    [NotMapped]
    public bool IsDebugFont => Category.Equals("debug", StringComparison.OrdinalIgnoreCase);
}
