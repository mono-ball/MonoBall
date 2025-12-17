using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities.Base;

namespace MonoBallFramework.Game.GameData.Entities;

/// <summary>
///     EF Core entity for tile behavior definitions.
///     Stores tile behavior types with flags for fast lookup.
///     Replaces TypeRegistry&lt;TileBehaviorDefinition&gt;.
/// </summary>
[Table("TileBehaviors")]
public class TileBehaviorEntity : BaseEntity
{
    /// <summary>
    ///     Unique identifier for this tile behavior type.
    ///     Example: "base:tile_behavior:movement/jump_south", "base:tile_behavior:movement/ice"
    /// </summary>
    [Key]
    [Column(TypeName = "nvarchar(100)")]
    public GameTileBehaviorId TileBehaviorId { get; set; } = GameTileBehaviorId.CreateMovement("default");

    /// <summary>
    ///     Behavior flags for fast lookup without script execution.
    ///     Stored as integer representation of TileBehaviorFlags enum.
    /// </summary>
    public int Flags { get; set; } = 0;

    /// <summary>
    ///     Path to the Roslyn .csx script file that implements this behavior.
    ///     Relative to the Scripts directory.
    ///     Example: "tiles/ice_slide_behavior.csx"
    /// </summary>
    [MaxLength(500)]
    public string? BehaviorScript { get; set; }

    // Computed properties

    /// <summary>
    ///     Gets the behavior flags as the enum type.
    /// </summary>
    [NotMapped]
    public TileBehaviorFlags BehaviorFlags
    {
        get => (TileBehaviorFlags)Flags;
        set => Flags = (int)value;
    }

    /// <summary>
    ///     Gets whether this tile can trigger wild Pokemon encounters.
    /// </summary>
    [NotMapped]
    public bool HasEncounters => BehaviorFlags.HasFlag(TileBehaviorFlags.HasEncounters);

    /// <summary>
    ///     Gets whether this tile requires Surf HM to traverse.
    /// </summary>
    [NotMapped]
    public bool IsSurfable => BehaviorFlags.HasFlag(TileBehaviorFlags.Surfable);

    /// <summary>
    ///     Gets whether this tile blocks all movement.
    /// </summary>
    [NotMapped]
    public bool BlocksMovement => BehaviorFlags.HasFlag(TileBehaviorFlags.BlocksMovement);

    /// <summary>
    ///     Gets whether this tile forces automatic movement.
    /// </summary>
    [NotMapped]
    public bool ForcesMovement => BehaviorFlags.HasFlag(TileBehaviorFlags.ForcesMovement);
}
