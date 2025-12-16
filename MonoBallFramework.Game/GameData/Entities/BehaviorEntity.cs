using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.Entities;

/// <summary>
///     EF Core entity for NPC behavior definitions.
///     Stores behavior types and their associated Roslyn scripts.
///     Replaces TypeRegistry&lt;BehaviorDefinition&gt;.
/// </summary>
[Table("Behaviors")]
public class BehaviorEntity
{
    /// <summary>
    ///     Unique identifier for this behavior type.
    ///     Example: "base:behavior:npc/patrol", "base:behavior:npc/stationary"
    /// </summary>
    [Key]
    [Column(TypeName = "nvarchar(100)")]
    public GameBehaviorId BehaviorId { get; set; } = GameBehaviorId.CreateNpcBehavior("default");

    /// <summary>
    ///     Display name for this behavior.
    ///     Example: "Patrol Behavior"
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Description of what this behavior does.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

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

    /// <summary>
    ///     JSON-serialized extension data from mods.
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ExtensionData { get; set; }

    /// <summary>
    ///     Gets whether this behavior is from a mod.
    /// </summary>
    [NotMapped]
    public bool IsFromMod => !string.IsNullOrEmpty(SourceMod);

    /// <summary>
    ///     Gets the extension data as a parsed dictionary.
    /// </summary>
    [NotMapped]
    public Dictionary<string, JsonElement>? ParsedExtensionData
    {
        get
        {
            if (string.IsNullOrEmpty(ExtensionData))
                return null;
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ExtensionData);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    ///     Gets a custom property value from extension data.
    /// </summary>
    public T? GetExtensionProperty<T>(string propertyName)
    {
        var data = ParsedExtensionData;
        if (data == null || !data.TryGetValue(propertyName, out var element))
            return default;
        try
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        catch
        {
            return default;
        }
    }
}
