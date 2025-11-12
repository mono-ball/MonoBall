using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PokeSharp.Game.Data.Entities;

/// <summary>
/// EF Core entity for map definitions.
/// Stores Tiled map data and metadata for runtime map loading.
/// </summary>
[Table("Maps")]
public class MapDefinition
{
    /// <summary>
    /// Unique map identifier (e.g., "littleroot_town", "route_101").
    /// </summary>
    [Key]
    [MaxLength(100)]
    public string MapId { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown in-game (e.g., "Littleroot Town", "Route 101").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Region this map belongs to (e.g., "hoenn", "kanto").
    /// </summary>
    [MaxLength(50)]
    public string Region { get; set; } = "hoenn";

    /// <summary>
    /// Map type for categorization (e.g., "town", "route", "cave", "building").
    /// </summary>
    [MaxLength(50)]
    public string? MapType { get; set; }

    /// <summary>
    /// Complete Tiled JSON data stored as string.
    /// This is the full TmxDocument that MapLoader will parse.
    /// </summary>
    [Required]
    public string TiledDataJson { get; set; } = "{}";

    /// <summary>
    /// Background music track ID.
    /// </summary>
    [MaxLength(100)]
    public string? MusicId { get; set; }

    /// <summary>
    /// Default weather (e.g., "clear", "rain", "sandstorm").
    /// </summary>
    [MaxLength(50)]
    public string Weather { get; set; } = "clear";

    /// <summary>
    /// Show map name on screen when entering.
    /// </summary>
    public bool ShowMapName { get; set; } = true;

    /// <summary>
    /// Can fly to this map.
    /// </summary>
    public bool CanFly { get; set; } = false;

    /// <summary>
    /// Background image for parallax scrolling (optional).
    /// </summary>
    [MaxLength(200)]
    public string? BackgroundImage { get; set; }

    /// <summary>
    /// Map connected to the north.
    /// </summary>
    [MaxLength(100)]
    public string? NorthMapId { get; set; }

    /// <summary>
    /// Map connected to the south.
    /// </summary>
    [MaxLength(100)]
    public string? SouthMapId { get; set; }

    /// <summary>
    /// Map connected to the east.
    /// </summary>
    [MaxLength(100)]
    public string? EastMapId { get; set; }

    /// <summary>
    /// Map connected to the west.
    /// </summary>
    [MaxLength(100)]
    public string? WestMapId { get; set; }

    /// <summary>
    /// Wild Pokémon encounter data as JSON.
    /// Future: Replace with proper EncounterTable join table.
    /// </summary>
    public string? EncounterDataJson { get; set; }

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

    // Navigation properties for map connections (optional, for complex queries)
    // Commented out for now to avoid circular references
    // [ForeignKey(nameof(NorthMapId))]
    // public MapDefinition? NorthMap { get; set; }

    // [ForeignKey(nameof(SouthMapId))]
    // public MapDefinition? SouthMap { get; set; }

    // [ForeignKey(nameof(EastMapId))]
    // public MapDefinition? EastMap { get; set; }

    // [ForeignKey(nameof(WestMapId))]
    // public MapDefinition? WestMap { get; set; }
}

