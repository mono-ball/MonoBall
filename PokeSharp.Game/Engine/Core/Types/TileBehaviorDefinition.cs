using System.Text.Json.Serialization;

namespace PokeSharp.Game.Engine.Core.Types;

/// <summary>
///     Type definition for tile behaviors.
///     Loaded from JSON and used by the TypeRegistry system.
/// </summary>
public record TileBehaviorDefinition : IScriptedType
{
    /// <summary>
    ///     Behavior flags (for fast lookup without script execution).
    ///     Similar to Pokemon Emerald's sTileBitAttributes.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TileBehaviorFlags Flags { get; init; } = TileBehaviorFlags.None;

    /// <summary>
    ///     Unique identifier for this behavior type (e.g., "jump_south", "ice", "tall_grass").
    /// </summary>
    public required string TypeId { get; init; }

    /// <summary>
    ///     Display name for this behavior.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    ///     Description of what this behavior does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///     Path to the Roslyn .csx script file that implements this behavior.
    ///     Relative to the Scripts directory.
    /// </summary>
    public string? BehaviorScript { get; init; }
}

/// <summary>
///     Flags for tile behaviors to enable fast checks without script execution.
/// </summary>
[Flags]
public enum TileBehaviorFlags
{
    /// <summary>
    ///     No special flags.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Can trigger wild Pokemon encounters.
    /// </summary>
    HasEncounters = 1 << 0,

    /// <summary>
    ///     Requires Surf HM to traverse.
    /// </summary>
    Surfable = 1 << 1,

    /// <summary>
    ///     Blocks all movement (impassable).
    /// </summary>
    BlocksMovement = 1 << 2,

    /// <summary>
    ///     Forces automatic movement (ice, currents).
    /// </summary>
    ForcesMovement = 1 << 3,

    /// <summary>
    ///     Prevents running on this tile.
    /// </summary>
    DisablesRunning = 1 << 4,
}
