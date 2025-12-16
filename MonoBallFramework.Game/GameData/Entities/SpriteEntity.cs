using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.Entities;

/// <summary>
///     EF Core entity for sprite definitions.
///     Stores sprite metadata, frame definitions, and animation data.
///     Replaces direct JSON loading in SpriteRegistry.
/// </summary>
[Table("Sprites")]
public class SpriteEntity
{
    /// <summary>
    ///     Unique sprite identifier in unified format.
    ///     Example: "base:sprite:npcs/elite_four/drake"
    /// </summary>
    [Key]
    [MaxLength(150)]
    [Column(TypeName = "nvarchar(150)")]
    public GameSpriteId SpriteId { get; set; } = null!;

    /// <summary>
    ///     Human-readable display name.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Asset type - always "Sprite" for sprite definitions.
    /// </summary>
    [MaxLength(50)]
    public string Type { get; set; } = "Sprite";

    /// <summary>
    ///     Relative path to texture from Assets root.
    ///     Example: "Graphics/Sprites/npcs/elite_four/drake.png"
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string TexturePath { get; set; } = string.Empty;

    /// <summary>
    ///     Width of each frame in pixels.
    /// </summary>
    public int FrameWidth { get; set; }

    /// <summary>
    ///     Height of each frame in pixels.
    /// </summary>
    public int FrameHeight { get; set; }

    /// <summary>
    ///     Total number of frames in the sprite sheet.
    /// </summary>
    public int FrameCount { get; set; }

    /// <summary>
    ///     Frame definitions stored as JSON column via EF Core OwnsMany().ToJson().
    ///     Each frame contains: Index, X, Y, Width, Height
    /// </summary>
    public List<SpriteFrame> Frames { get; set; } = new();

    /// <summary>
    ///     Animation definitions stored as JSON column via EF Core OwnsMany().ToJson().
    ///     Each animation contains: Name, Loop, FrameIndices[], FrameDurations[], FlipHorizontal
    /// </summary>
    public List<SpriteAnimation> Animations { get; set; } = new();

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

    // Computed properties (not stored in DB)

    /// <summary>
    ///     Gets the sprite category from the ID (e.g., "npcs" from "base:sprite:npcs/elite_four/drake")
    /// </summary>
    [NotMapped]
    public string SpriteCategory => SpriteId.Category;

    /// <summary>
    ///     Gets the subcategory from the ID (e.g., "elite_four" from "base:sprite:npcs/elite_four/drake")
    /// </summary>
    [NotMapped]
    public string? SpriteSubcategory => SpriteId.Subcategory;

    /// <summary>
    ///     Gets the sprite name from the ID (e.g., "drake" from "base:sprite:npcs/elite_four/drake")
    /// </summary>
    [NotMapped]
    public string SpriteName => SpriteId.Name;
}

/// <summary>
///     Owned entity for sprite frame definitions.
///     Stored as JSON column in SpriteEntity.
/// </summary>
public class SpriteFrame
{
    /// <summary>
    ///     Frame index in the sprite sheet.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     X position in the sprite sheet (pixels).
    /// </summary>
    public int X { get; set; }

    /// <summary>
    ///     Y position in the sprite sheet (pixels).
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    ///     Width of the frame (pixels).
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     Height of the frame (pixels).
    /// </summary>
    public int Height { get; set; }
}

/// <summary>
///     Owned entity for sprite animation definitions.
///     Stored as JSON column in SpriteEntity.
/// </summary>
public class SpriteAnimation
{
    /// <summary>
    ///     Animation name (e.g., "walk_down", "idle_up").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Whether the animation should loop.
    /// </summary>
    public bool Loop { get; set; }

    /// <summary>
    ///     Indices of frames in this animation.
    /// </summary>
    public List<int> FrameIndices { get; set; } = new();

    /// <summary>
    ///     Duration of each frame in seconds.
    /// </summary>
    public List<double> FrameDurations { get; set; } = new();

    /// <summary>
    ///     Whether to flip the sprite horizontally.
    /// </summary>
    public bool FlipHorizontal { get; set; }
}
