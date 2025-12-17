using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities.Base;

namespace MonoBallFramework.Game.GameData.Entities;

/// <summary>
///     EF Core entity for map definitions.
///     Stores Tiled map data and metadata for runtime map loading.
/// </summary>
[Table("Maps")]
public class MapEntity : BaseEntity
{
    /// <summary>
    ///     Unique map identifier in unified format (e.g., "base:map:hoenn/littleroot_town").
    /// </summary>
    [Key]
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public GameMapId MapId { get; set; } = null!;

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
    ///     Relative path to Tiled JSON file (e.g., "Definitions/Maps/littleroot_town.json").
    ///     MapLoader will read the file at runtime to parse TmxDocument.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string TiledDataPath { get; set; } = string.Empty;

    /// <summary>
    ///     Background music track ID (e.g., "base:audio:music/towns/mus_littleroot").
    /// </summary>
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public GameAudioId? MusicId { get; set; }

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
    ///     Requires Flash HM to see properly (dark caves).
    /// </summary>
    public bool RequiresFlash { get; set; } = false;

    /// <summary>
    ///     Allow player to run (hold B button).
    /// </summary>
    public bool AllowRunning { get; set; } = true;

    /// <summary>
    ///     Allow player to use bicycle.
    /// </summary>
    public bool AllowCycling { get; set; } = true;

    /// <summary>
    ///     Allow player to use Escape Rope or Dig to leave.
    /// </summary>
    public bool AllowEscaping { get; set; } = false;

    /// <summary>
    ///     Battle scene/background for wild encounters (e.g., "MAP_BATTLE_SCENE_NORMAL").
    /// </summary>
    [MaxLength(50)]
    public string BattleScene { get; set; } = "MAP_BATTLE_SCENE_NORMAL";

    /// <summary>
    ///     Region map section for Town Map highlighting (e.g., "base:mapsec:hoenn/littleroot_town").
    /// </summary>
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public GameMapSectionId? RegionMapSection { get; set; }

    /// <summary>
    ///     Map connected to the north (e.g., "base:map:hoenn/route_101").
    /// </summary>
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public GameMapId? NorthMapId { get; set; }

    /// <summary>
    ///     Connection offset for north map in tiles.
    ///     Shifts the connected map horizontally (positive = right, negative = left).
    /// </summary>
    public int NorthConnectionOffset { get; set; } = 0;

    /// <summary>
    ///     Map connected to the south (e.g., "base:map:hoenn/route_102").
    /// </summary>
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public GameMapId? SouthMapId { get; set; }

    /// <summary>
    ///     Connection offset for south map in tiles.
    ///     Shifts the connected map horizontally (positive = right, negative = left).
    /// </summary>
    public int SouthConnectionOffset { get; set; } = 0;

    /// <summary>
    ///     Map connected to the east (e.g., "base:map:hoenn/oldale_town").
    /// </summary>
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public GameMapId? EastMapId { get; set; }

    /// <summary>
    ///     Connection offset for east map in tiles.
    ///     Shifts the connected map vertically (positive = down, negative = up).
    /// </summary>
    public int EastConnectionOffset { get; set; } = 0;

    /// <summary>
    ///     Map connected to the west (e.g., "base:map:hoenn/petalburg_city").
    /// </summary>
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public GameMapId? WestMapId { get; set; }

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

    // Navigation properties for map connections (optional, for complex queries)
    // Commented out for now to avoid circular references
    // [ForeignKey(nameof(NorthMapId))]
    // public MapEntity? NorthMap { get; set; }

    // [ForeignKey(nameof(SouthMapId))]
    // public MapEntity? SouthMap { get; set; }

    // [ForeignKey(nameof(EastMapId))]
    // public MapEntity? EastMap { get; set; }

    // [ForeignKey(nameof(WestMapId))]
    // public MapEntity? WestMap { get; set; }
}
