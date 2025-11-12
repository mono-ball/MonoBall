using System.ComponentModel.DataAnnotations;

namespace PokeSharp.Game.Data.Entities;

/// <summary>
/// EF Core entity for NPC definitions.
/// Represents reusable NPC data (dialogue, behaviors, appearance).
/// </summary>
public class NpcDefinition
{
    /// <summary>
    /// Unique identifier (e.g., "npc/youngster_joey", "npc/prof_birch").
    /// </summary>
    [Key]
    [MaxLength(100)]
    public string NpcId { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown in-game (e.g., "YOUNGSTER JOEY", "PROF. BIRCH").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// NPC type for categorization (e.g., "trainer", "shopkeeper", "generic").
    /// </summary>
    [MaxLength(50)]
    public string? NpcType { get; set; }

    /// <summary>
    /// Sprite/appearance ID (references sprite in AssetManager).
    /// </summary>
    [MaxLength(100)]
    public string? SpriteId { get; set; }

    /// <summary>
    /// Default behavior script path (e.g., "Behaviors/wander_behavior.csx").
    /// </summary>
    [MaxLength(200)]
    public string? BehaviorScript { get; set; }

    /// <summary>
    /// Dialogue script path (e.g., "Dialogue/youngster_joey.csx").
    /// </summary>
    [MaxLength(200)]
    public string? DialogueScript { get; set; }

    /// <summary>
    /// Default movement speed (tiles per second).
    /// </summary>
    public float MovementSpeed { get; set; } = 2.0f;

    /// <summary>
    /// Custom properties as JSON (extensible for modding).
    /// </summary>
    public string? CustomPropertiesJson { get; set; }

    /// <summary>
    /// Source mod ID (null for base game).
    /// </summary>
    [MaxLength(100)]
    public string? SourceMod { get; set; }

    /// <summary>
    /// Version for compatibility tracking.
    /// </summary>
    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";
}

