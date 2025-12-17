using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities.Base;

namespace MonoBallFramework.Game.GameData.Entities;

/// <summary>
///     EF Core entity for popup outline/border definitions.
///     Stores outline styles for map region/location popups.
///     Supports both tile sheet rendering (GBA-accurate) and legacy 9-slice rendering.
///     Replaces direct JSON loading in PopupRegistry.
/// </summary>
[Table("PopupOutlines")]
public class PopupOutlineEntity : BaseEntity
{
    /// <summary>
    ///     Unique identifier for this outline style.
    ///     Example: "base:popup:outline/stone_outline"
    /// </summary>
    [Key]
    [Column(TypeName = "nvarchar(150)")]
    public GamePopupOutlineId OutlineId { get; set; } = GamePopupOutlineId.Create("default");

    /// <summary>
    ///     Type of rendering: "TileSheet" for GBA-accurate, "9Slice" for legacy.
    /// </summary>
    [MaxLength(50)]
    public string Type { get; set; } = "TileSheet";

    /// <summary>
    ///     Path to the outline texture (relative to asset root).
    ///     Example: "Graphics/Maps/Popups/Outlines/stone_outline.png"
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string TexturePath { get; set; } = string.Empty;

    // === Tile Sheet Properties (GBA-accurate) ===

    /// <summary>
    ///     Width of each tile in pixels (typically 8 for GBA).
    /// </summary>
    public int TileWidth { get; set; } = 8;

    /// <summary>
    ///     Height of each tile in pixels (typically 8 for GBA).
    /// </summary>
    public int TileHeight { get; set; } = 8;

    /// <summary>
    ///     Total number of tiles in the tile sheet.
    /// </summary>
    public int TileCount { get; set; }

    /// <summary>
    ///     Tile definitions stored as JSON column via EF Core OwnsMany().ToJson().
    ///     Each tile contains: Index, X, Y, Width, Height
    /// </summary>
    public List<OutlineTile> Tiles { get; set; } = [];

    /// <summary>
    ///     Tile usage mapping stored as JSON column via EF Core OwnsOne().ToJson().
    ///     Contains: TopEdge[], LeftEdge[], RightEdge[], BottomEdge[]
    /// </summary>
    public OutlineTileUsage? TileUsage { get; set; }

    // === Legacy 9-Slice Properties (backwards compatibility) ===

    /// <summary>
    ///     Width of the corner slices in pixels (for legacy 9-slice rendering).
    /// </summary>
    public int CornerWidth { get; set; } = 8;

    /// <summary>
    ///     Height of the corner slices in pixels (for legacy 9-slice rendering).
    /// </summary>
    public int CornerHeight { get; set; } = 8;

    /// <summary>
    ///     Width of the border frame in pixels (for legacy 9-slice rendering).
    /// </summary>
    public int BorderWidth { get; set; } = 8;

    /// <summary>
    ///     Height of the border frame in pixels (for legacy 9-slice rendering).
    /// </summary>
    public int BorderHeight { get; set; } = 8;

    // Computed properties

    /// <summary>
    ///     Gets the theme name from the ID (e.g., "stone_outline" from "base:popup:outline/stone_outline")
    /// </summary>
    [NotMapped]
    public string ThemeName => OutlineId.Name;

    /// <summary>
    ///     Gets whether this outline uses tile sheet rendering.
    /// </summary>
    [NotMapped]
    public bool IsTileSheet => Type == "TileSheet" && Tiles.Count > 0 && TileUsage != null;
}

/// <summary>
///     Owned entity for outline tile definitions.
///     Stored as JSON column in PopupOutlineEntity.
/// </summary>
public class OutlineTile
{
    /// <summary>
    ///     Tile index in the tile sheet.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     X position in the tile sheet (pixels).
    /// </summary>
    public int X { get; set; }

    /// <summary>
    ///     Y position in the tile sheet (pixels).
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    ///     Width of the tile (pixels).
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     Height of the tile (pixels).
    /// </summary>
    public int Height { get; set; }
}

/// <summary>
///     Owned entity for outline tile usage mapping.
///     Defines which tiles are used for each edge of the popup border.
///     Stored as JSON column in PopupOutlineEntity.
/// </summary>
public class OutlineTileUsage
{
    /// <summary>
    ///     Tile indices for the top edge.
    /// </summary>
    public List<int> TopEdge { get; set; } = [];

    /// <summary>
    ///     Tile indices for the left edge.
    /// </summary>
    public List<int> LeftEdge { get; set; } = [];

    /// <summary>
    ///     Tile indices for the right edge.
    /// </summary>
    public List<int> RightEdge { get; set; } = [];

    /// <summary>
    ///     Tile indices for the bottom edge.
    /// </summary>
    public List<int> BottomEdge { get; set; } = [];
}
