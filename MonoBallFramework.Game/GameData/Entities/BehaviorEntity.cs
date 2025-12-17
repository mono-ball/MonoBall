using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities.Base;

namespace MonoBallFramework.Game.GameData.Entities;

/// <summary>
///     EF Core entity for NPC behavior definitions.
///     Stores behavior types and their associated Roslyn scripts.
///     Replaces TypeRegistry&lt;BehaviorDefinition&gt;.
/// </summary>
[Table("Behaviors")]
public class BehaviorEntity : BaseEntity
{
    /// <summary>
    ///     Unique identifier for this behavior type.
    ///     Example: "base:behavior:npc/patrol", "base:behavior:npc/stationary"
    /// </summary>
    [Key]
    [Column(TypeName = "nvarchar(100)")]
    public GameBehaviorId BehaviorId { get; set; } = GameBehaviorId.CreateNpcBehavior("default");

    /// <summary>
    ///     Default movement speed in tiles per second.
    /// </summary>
    public float DefaultSpeed { get; set; } = 4.0f;

    /// <summary>
    ///     Time to pause at waypoints (in seconds).
    /// </summary>
    public float PauseAtWaypoint { get; set; } = 1.0f;

    /// <summary>
    ///     Whether this behavior allows NPC interaction while moving.
    /// </summary>
    public bool AllowInteractionWhileMoving { get; set; } = false;

    /// <summary>
    ///     Path to the Roslyn .csx script file that implements this behavior.
    ///     Relative to the Scripts directory.
    ///     Example: "behaviors/patrol_behavior.csx"
    /// </summary>
    [MaxLength(500)]
    public string? BehaviorScript { get; set; }
}
