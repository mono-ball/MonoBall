using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PokeSharp.Game.Engine.Core.Types;

namespace PokeSharp.Game.Data.Entities;

/// <summary>
///     EF Core entity for trainer definitions.
///     Represents trainers with parties, AI, and battle data.
/// </summary>
public class TrainerDefinition
{
    /// <summary>
    ///     Unique identifier (e.g., "trainer/youngster_joey", "trainer/roxanne_1").
    /// </summary>
    [Key]
    [MaxLength(100)]
    public string TrainerId { get; set; } = string.Empty;

    /// <summary>
    ///     Display name (e.g., "YOUNGSTER JOEY", "ROXANNE").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    ///     Trainer class (e.g., "youngster", "gym_leader", "rival").
    /// </summary>
    [MaxLength(50)]
    public string TrainerClass { get; set; } = "trainer";

    /// <summary>
    ///     Sprite ID for battle sprite.
    /// </summary>
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public SpriteId? SpriteId { get; set; }

    /// <summary>
    ///     Prize money (base amount, multiplied by highest level).
    /// </summary>
    public int PrizeMoney { get; set; } = 100;

    /// <summary>
    ///     Items the trainer uses in battle (comma-separated for now).
    ///     TODO: Replace with proper join table when Item entities exist.
    /// </summary>
    [MaxLength(500)]
    public string? Items { get; set; }

    /// <summary>
    ///     AI script path (e.g., "AI/basic_trainer.csx").
    /// </summary>
    [MaxLength(200)]
    public string? AiScript { get; set; }

    /// <summary>
    ///     Dialogue before battle.
    /// </summary>
    [MaxLength(500)]
    public string? IntroDialogue { get; set; }

    /// <summary>
    ///     Dialogue when defeated.
    /// </summary>
    [MaxLength(500)]
    public string? DefeatDialogue { get; set; }

    /// <summary>
    ///     Script to run when defeated (e.g., "Events/roxanne_defeat.csx").
    /// </summary>
    [MaxLength(200)]
    public string? OnDefeatScript { get; set; }

    /// <summary>
    ///     Can battle this trainer multiple times.
    /// </summary>
    public bool IsRematchable { get; set; } = false;

    /// <summary>
    ///     Party data stored as JSON (simplified for now).
    ///     TODO: Replace with proper TrainerParty join table when Pokemon entities exist.
    /// </summary>
    [Required]
    public string PartyJson { get; set; } = "[]";

    /// <summary>
    ///     Custom properties as JSON (extensible).
    /// </summary>
    public string? CustomPropertiesJson { get; set; }

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
}

/// <summary>
///     DTO for trainer party member (used in PartyJson).
///     Will be replaced with proper EF entities when Pokemon system is added.
/// </summary>
public record TrainerPartyMemberDto
{
    public string Species { get; init; } = string.Empty;
    public int Level { get; init; }
    public string[]? Moves { get; init; }
    public string? HeldItem { get; init; }
    public string? Ability { get; init; }
    public Dictionary<string, int>? Ivs { get; init; }
}
