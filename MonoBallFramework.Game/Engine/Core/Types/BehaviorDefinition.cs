using System.Text.Json.Serialization;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Type definition for NPC behaviors.
///     Loaded from JSON and used by the TypeRegistry system.
/// </summary>
public record BehaviorDefinition : IScriptedType
{
    /// <summary>
    ///     Default movement speed in tiles per second.
    /// </summary>
    public float DefaultSpeed { get; init; } = 4.0f;

    /// <summary>
    ///     Time to pause at waypoints (in seconds).
    /// </summary>
    public float PauseAtWaypoint { get; init; } = 1.0f;

    /// <summary>
    ///     Whether this behavior allows the NPC to be interacted with while moving.
    /// </summary>
    public bool AllowInteractionWhileMoving { get; init; }

    /// <summary>
    ///     Unique identifier for this behavior type (e.g., "patrol", "stationary", "trainer").
    /// </summary>
    [JsonPropertyName("id")]
    [JsonRequired]
    public required string DefinitionId { get; init; }

    /// <summary>
    ///     Name for this behavior (e.g., "Patrol Behavior").
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

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
