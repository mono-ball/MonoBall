using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Game.Data.Entities;

/// <summary>
///     EF Core entity for map definitions.
///     Stores Tiled map data and metadata for runtime map loading.
/// </summary>
[Table("Maps")]
public class MapDefinition
{
    /// <summary>
    ///     Unique map identifier (e.g., "littleroot_town", "route_101").
    /// </summary>
    [Key]
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public MapIdentifier MapId { get; set; }

    /// <summary>
    ///     Display name shown in-game (e.g., "Littleroot Town", "Route 101").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    ///     Region this map belongs to (e.g., "hoenn", "kanto").
    /// </summary>
    [MaxLength(50)]
    public string Region { get; set; } = "hoenn";

    /// <summary>
    ///     Map type for categorization (e.g., "town", "route", "cave", "building").
    /// </summary>
    [MaxLength(50)]
    public string? MapType { get; set; }

    /// <summary>
    ///     Relative path to Tiled JSON file (e.g., "Data/Maps/littleroot_town.json").
    ///     MapLoader will read the file at runtime to parse TmxDocument.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string TiledDataPath { get; set; } = string.Empty;

    /// <summary>
    ///     Background music track ID.
    /// </summary>
    [MaxLength(100)]
    public string? MusicId { get; set; }

    /// <summary>
    ///     Default weather (e.g., "clear", "rain", "sandstorm").
    /// </summary>
    [MaxLength(50)]
    public string Weather { get; set; } = "clear";

    /// <summary>
    ///     Show map name on screen when entering.
    /// </summary>
    public bool ShowMapName { get; set; } = true;

    /// <summary>
    ///     Can fly to this map.
    /// </summary>
    public bool CanFly { get; set; } = false;

    /// <summary>
    ///     Background image for parallax scrolling (optional).
    /// </summary>
    [MaxLength(200)]
    public string? BackgroundImage { get; set; }

    /// <summary>
    ///     Map connected to the north.
    /// </summary>
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public MapIdentifier? NorthMapId { get; set; }

    /// <summary>
    ///     Connection offset for north map in tiles.
    ///     Shifts the connected map horizontally (positive = right, negative = left).
    /// </summary>
    public int NorthConnectionOffset { get; set; } = 0;

    /// <summary>
    ///     Map connected to the south.
    /// </summary>
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public MapIdentifier? SouthMapId { get; set; }

    /// <summary>
    ///     Connection offset for south map in tiles.
    ///     Shifts the connected map horizontally (positive = right, negative = left).
    /// </summary>
    public int SouthConnectionOffset { get; set; } = 0;

    /// <summary>
    ///     Map connected to the east.
    /// </summary>
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public MapIdentifier? EastMapId { get; set; }

    /// <summary>
    ///     Connection offset for east map in tiles.
    ///     Shifts the connected map vertically (positive = down, negative = up).
    /// </summary>
    public int EastConnectionOffset { get; set; } = 0;

    /// <summary>
    ///     Map connected to the west.
    /// </summary>
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public MapIdentifier? WestMapId { get; set; }

    /// <summary>
    ///     Connection offset for west map in tiles.
    ///     Shifts the connected map vertically (positive = down, negative = up).
    /// </summary>
    public int WestConnectionOffset { get; set; } = 0;

    /// <summary>
    ///     Wild Pokémon encounter data as JSON.
    ///     Future: Replace with proper EncounterTable join table.
    /// </summary>
    public string? EncounterDataJson { get; set; }

    /// <summary>
    ///     Custom properties as JSON (extensible for modding).
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
